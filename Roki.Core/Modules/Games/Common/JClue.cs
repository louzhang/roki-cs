using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Roki.Common;
using Roki.Extensions;

namespace Roki.Modules.Games.Common
{
    public class JClue
    {
        public string Category { get; set; }
        public string Clue { get; set; }
        public string Answer { get; set; }
        public int Value { get; set; }
        public bool Available { get; set; } = true;

        private string _minAnswer;
        private readonly List<string> _acceptedAnswers = new List<string>();

        public void SanitizeAnswer()
        {
            var minAnswer = Regex.Replace(Answer.ToLowerInvariant(), "^(the |a |an )", "")
                .Replace(" the ", "")
                .Replace(" an ", "")
                .Replace(" a ", "");
            
            var convert = new ConvertASCII();
            convert.FoldToASCII(minAnswer.ToCharArray(), minAnswer.Length);
            minAnswer = new string(convert.Output).Trim('\0');

            if (minAnswer.StartsWith("(1 of)", StringComparison.Ordinal))
            {
                var answers = minAnswer.Replace("(1 of) ", "").Split(", ");
                foreach (var answer in answers)
                {
                    _acceptedAnswers.Add(answer.ToLowerInvariant().SanitizeStringFull());
                }
                _minAnswer = minAnswer.SanitizeStringFull();
                return;
            }
            if (minAnswer.StartsWith("(2 of)", StringComparison.Ordinal))
            {
                var answers = minAnswer.Replace("(2 of) ", "").Split(", ");
                foreach (var answer in answers)
                {
                    _acceptedAnswers.Add(answer.ToLowerInvariant().SanitizeStringFull().SanitizeStringFull());
                }
                return;
            }
            if (minAnswer.StartsWith("(3 of)", StringComparison.Ordinal))
            {
                var answers = minAnswer.Replace("(3 of) ", "").Split(", ");
                foreach (var answer in answers)
                {
                    _acceptedAnswers.Add(answer.ToLowerInvariant().SanitizeStringFull().SanitizeStringFull());
                }
                return;
            }

            if (minAnswer.Contains("/", StringComparison.Ordinal))
            {
                var answers = minAnswer.Split("/");
                foreach (var answer in answers)
                {
                    if (answer.Length < 2) continue;
                    _acceptedAnswers.Add(answer.SanitizeStringFull());
                }
            }

            if (minAnswer.Contains(" and ", StringComparison.Ordinal))
            {
                var answers = minAnswer.Split(" and ");
                foreach (var answer in answers)
                {
                    _acceptedAnswers.Add(answer.SanitizeStringFull());
                }
            }
            else if (minAnswer.Contains(" & ", StringComparison.Ordinal))
            {
                var answers = minAnswer.Split(" & ");
                foreach (var answer in answers)
                {
                    _acceptedAnswers.Add(answer.SanitizeStringFull());
                }
            }
            
            // if it contains an optional answer
            if (Answer.Contains('(', StringComparison.Ordinal) && Answer.Contains(')', StringComparison.Ordinal))
            {
                // currently this wont be correctly split: "termite (in term itemize) (mite accepted)"
                var optional = minAnswer.Split('(', ')')[1];
                
                // example: "cruisers (or ships)"
                if (optional.StartsWith("or", StringComparison.Ordinal))
                {
                    var optionals = optional.Split("or");
                    foreach (var op in optionals)
                    {
                        // 2nd condition example "mare(s or maria)"
                        if (string.IsNullOrWhiteSpace(op) || op.SanitizeStringFull().Length < 2) continue;
                        _acceptedAnswers.Add(op.SanitizeStringFull());
                    }
                }
                // example: "endurance (durability accepted)"
                else if (optional.EndsWith("accepted", StringComparison.Ordinal))
                {
                    _acceptedAnswers.Add(Regex.Replace(optional, " also accepted| accepted$", "").SanitizeStringFull());
                }
                // example: "The Daily Planet ("Superman")"
                else if (optional.Contains('"', StringComparison.Ordinal))
                {
                    _acceptedAnswers.Add(optional.Split('"', '"')[1].SanitizeStringFull());
                }
                // this one is kinda hard to do since there are cases where it isn't valid
                // valid example added: "MoMA (the Museum of Modern Art)"
                // not valid example but added: "(the University of) Chicago", "the (San Francisco) 49ers"
                // valid example but not added: "Republic of Korea (South Korea)"
                else if (optional.SanitizeStringFull().Length > minAnswer.SanitizeStringFull().Length * 1.5)
                {
                    _acceptedAnswers.Add(optional.SanitizeStringFull());
                }
                _acceptedAnswers.Add(minAnswer.SanitizeStringFull());

                minAnswer = Regex.Replace(minAnswer, @"\(.*?\)", "");
            }

            _minAnswer = minAnswer.SanitizeStringFull();
        }

        public bool CheckAnswer(string answer)
        {
            var convert = new ConvertASCII();
            convert.FoldToASCII(answer.ToCharArray(), answer.Length);
            answer = new string(convert.Output).Trim('\0');
            
            if (Answer.StartsWith("(2 of)", StringComparison.Ordinal) || Answer.StartsWith("(3 of)", StringComparison.Ordinal))
            {
                var answers = SanitizeAnswerToList(answer);
                if (answers == null) return false;
                var correct = 0;
                foreach (var optionalAnswer in _acceptedAnswers)
                {
                    var ansLev = new Levenshtein(optionalAnswer);
                    foreach (var ans in answers)
                    {
                        if (ansLev.DistanceFrom(ans) <= Math.Round(optionalAnswer.Length * 0.1))
                        {
                            correct++;
                            // so they don't get points for submitting the same answer multiple times
                            break;
                        }
                    }
                }

                if (Answer.StartsWith("(2 of)", StringComparison.Ordinal))
                    return correct >= 2;
                if (Answer.StartsWith("(3 of)", StringComparison.Ordinal))
                    return correct >= 3;
            }

            var sanitizedAnswer = SanitizeAnswer(answer);

            if (_acceptedAnswers.Count > 0)
            {
                if (Answer.Contains(" and ", StringComparison.OrdinalIgnoreCase) || Answer.Contains(" & ", StringComparison.OrdinalIgnoreCase))
                {
                    var answers = SanitizeAnswerToList(answer);
                    var correct = 0;
                    foreach (var optionalAnswer in _acceptedAnswers)
                    {
                        var ansLev = new Levenshtein(optionalAnswer);
                        foreach (var ans in answers)
                        {
                            if (!(ansLev.DistanceFrom(ans) <= Math.Round(optionalAnswer.Length * 0.1))) continue;
                            correct++;
                            // so they don't get points for submitting the same answer multiple times
                            break;
                        }
                    }

                    if (answers.Count == correct) 
                        return true;
                }
                else
                {
                    var optLev = new Levenshtein(sanitizedAnswer);
                    if (_acceptedAnswers.Any(optionalAnswer => optLev.DistanceFrom(optionalAnswer) <= Math.Round(optionalAnswer.Length * 0.1)))
                        return true;
                }
            }

            var minLev = new Levenshtein(_minAnswer);
            var distance = minLev.DistanceFrom(sanitizedAnswer);
            if (distance == 0)
                return true;
            if (_minAnswer.Length <= 5)
                return distance == 0;
            if (_minAnswer.Length <= 9)
                return distance <= 1;

            return distance <= Math.Round(_minAnswer.Length * 0.15);
        }

        private static string SanitizeAnswer(string answer)
        {
            //remove all the?
            answer = answer.ToLowerInvariant();
            answer = Regex.Replace(answer, "^(what |whats |where |wheres |who |whos )", "");
            answer = Regex.Replace(answer, "^(is |are |was |were )", "");
            return Regex.Replace(answer, "^(the |a |an )", "").Replace(" and ", "", StringComparison.Ordinal).Replace(" the ", "").SanitizeStringFull();
        }

        private static List<string> SanitizeAnswerToList(string answer)
        {
            answer = answer.ToLowerInvariant();
            answer = Regex.Replace(answer, "^(what |whats |where |wheres |who |whos )", "");
            answer = Regex.Replace(answer, "^(is |are |was |were )", "");
            string[] guesses;
            if (answer.Contains(",", StringComparison.Ordinal))
            {
                guesses = answer.Split(",");
            }
            else if (answer.Contains(" and ", StringComparison.Ordinal))
            {
                guesses = answer.Split(" and ");
            }
            else if (answer.Contains(" & ", StringComparison.Ordinal))
            {
                guesses = answer.Split(" & ");
            }
            else
            {
                return null;
            }

            var answers = new List<string>();
            foreach (var guess in guesses)
            {
                if (string.IsNullOrWhiteSpace(guess)) continue;
                answers.Add(Regex.Replace(guess.Trim(), "^(the |a |an )", "").SanitizeStringFull());
            }

            return answers;
        }
    }
}