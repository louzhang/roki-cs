using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore.Internal;
using Roki.Common;
using Roki.Core.Services;
using Roki.Extensions;

namespace Roki.Modules.Games.Common
{
    public class Jeopardy
    {
        private SemaphoreSlim _guess = new SemaphoreSlim(1, 1);
        private readonly DbService _db;
        private readonly DiscordSocketClient _client;
        private readonly ICommandContext _ctx;
        private readonly Dictionary<string, List<JClue>> _clues;

        public IGuild Guild { get; }
        public ITextChannel Channel { get; }

        private CancellationTokenSource _cancel;
        
        public JClue CurrentClue { get; private set; }
        
        public ConcurrentDictionary<IUser, int> Users = new ConcurrentDictionary<IUser, int>();
        
        public bool CanGuess { get; private set; }
        public bool StopGame { get; private set; }
        private int _timeout = 0;
//        private Dictionary<int, bool> _choices1 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
//        private Dictionary<int, bool> _choices2 = new Dictionary<int, bool> {{200, false},{400, false},{600, false},{800, false},{1000, false}};
        
        public Jeopardy(DbService db, DiscordSocketClient client, Dictionary<string, List<JClue>> clues, IGuild guild, ITextChannel channel, ICommandContext ctx)
        {
            _db = db;
            _client = client;
            _clues = clues;
            
            Guild = guild;
            Channel = channel;
            _ctx = ctx;
        }

        public async Task StartGame()
        {
            await _ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                    .WithTitle("Jeopardy!")
                    .WithDescription(
                        "Welcome to Jeopardy!\nTo choose a category, please use the format `category for xxx`, you must specify full category name.\nResponses must be in question form"))
                .ConfigureAwait(false);
            while (!StopGame)
            {
                _cancel = new CancellationTokenSource();

                await ShowCategories().ConfigureAwait(false);
                var catResponse = await CategoryHandler().ConfigureAwait(false);
                var catStatus = ParseCategoryAndClue(catResponse);
                while (catStatus != CategoryStatus.Success)
                {
                    if (catStatus == CategoryStatus.UnavailableClue)
                        await _ctx.Channel.SendErrorAsync("That clue is not available, please try again").ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongAmount)
                        await _ctx.Channel.SendErrorAsync("There are no clues available for that amount, please try again")
                            .ConfigureAwait(false);
                    else if (catStatus == CategoryStatus.WrongCategory)
                        await _ctx.Channel.SendErrorAsync("No such category found, please try again").ConfigureAwait(false);
                    else
                    {
                        await _ctx.Channel.SendErrorAsync("No response received, stopped Jeopardy! game.").ConfigureAwait(false);
                        return;
                    }
                    
                    catResponse = await CategoryHandler().ConfigureAwait(false);
                    catStatus = ParseCategoryAndClue(catResponse);
                }
                
                // CurrentClue is now the chosen clue
                await _ctx.Channel.EmbedAsync(new EmbedBuilder().WithColor(Color.Blue)
                        .WithTitle($"{CurrentClue.Category} - {CurrentClue.Value}")
                        .WithDescription(CurrentClue.Clue))
                    .ConfigureAwait(false);

                try
                {
                    _client.MessageReceived += Guesses;
                    CanGuess = true;
                }
                finally
                {
                    CanGuess = false;
                    _client.MessageReceived -= Guesses;
                }
            }

        }

        private async Task ShowCategories()
        {
            var embed = new EmbedBuilder().WithColor(Color.Blue)
                .WithTitle("Jeopardy!")
                .WithDescription($"Please choose an available category and price from below.\ni.e. `{_clues.First().Key} for 200`");
            foreach (var (category, clues) in _clues)
            {
                embed.AddField(category, string.Join("\n", clues.Select(c => $"${(c.Available ? $"`{c.Value}`" : $"~~`{c.Value}`~~")}")));
            }
            
            await Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        private CategoryStatus ParseCategoryAndClue(IMessage msg)
        {
            if (msg == null) return CategoryStatus.NoResponse;
            
            var message = msg.Content.SanitizeStringFull().ToLowerInvariant();
            int.TryParse(new string(message.Where(char.IsDigit).ToArray()), out var amount);

            JClue clue;
            if (message.Contains(_clues.First().Key.SanitizeStringFull(), StringComparison.OrdinalIgnoreCase))
                clue = _clues.First().Value.FirstOrDefault(q => q.Value == amount);
            else if (message.Contains(_clues.First().Key.SanitizeStringFull(), StringComparison.OrdinalIgnoreCase))
                clue = _clues.First().Value.FirstOrDefault(q => q.Value == amount);
            else
                return CategoryStatus.WrongCategory;
            
            if (clue == null) return CategoryStatus.WrongAmount;
            if (!clue.Available) return CategoryStatus.UnavailableClue;
            CurrentClue = clue;
            return CategoryStatus.Success;
        }

        private Task Guesses(SocketMessage msg)
        {
            var _ = Task.Run(async () =>
            {
                if (msg.Author.IsBot || msg.Channel != Channel || !Regex.IsMatch(msg.Content, "^what|where|who")) return;
                var guess = false;
                await _guess.WaitAsync().ConfigureAwait(false);
                try
                {
                    if (CanGuess && CurrentClue.CheckAnswer(msg.Content) && !_cancel.IsCancellationRequested)
                    {
                        Users.AddOrUpdate(msg.Author, CurrentClue.Value, (u, old) => old + CurrentClue.Value);
                        guess = true;
                    }
                }
                finally
                {
                    _guess.Release();
                }
                if (!guess) return;
                _cancel.Cancel();
            });
            return Task.CompletedTask;
        }
        
        private async Task<SocketMessage> CategoryHandler(TimeSpan? timeout = null)
        {
            timeout ??= TimeSpan.FromMinutes(2);
            var eventTrigger = new TaskCompletionSource<SocketMessage>();
            var cancelTrigger = new TaskCompletionSource<bool>();

            Task Handler(SocketMessage message)
            {
                if (message.Channel.Id != Channel.Id || message.Author.IsBot) return Task.CompletedTask;
                var content = message.Content.SanitizeStringFull().ToLowerInvariant();
                if (!content.Contains("for", StringComparison.Ordinal) && !Regex.IsMatch(content, "\\d\\d\\d+"))
                    return Task.CompletedTask;
                // if (type == ReplyType.Guess && !Regex.IsMatch(content, "^what|where|who")) return Task.CompletedTask;

                eventTrigger.SetResult(message);
                return Task.CompletedTask;
            }
            
            _client.MessageReceived += Handler;

            var trigger = eventTrigger.Task;
            var cancel = cancelTrigger.Task;
            var delay = Task.Delay(timeout.Value);
            var task = await Task.WhenAny(trigger, cancel, delay).ConfigureAwait(false);

            _client.MessageReceived -= Handler;

            if (task == trigger)
                return await trigger.ConfigureAwait(false);
            return null;
        }
        
        private enum ReplyType
        {
            Category,
            Guess
        }
        
        private enum CategoryStatus
        {
            Success = 0,
            WrongAmount = -1,
            WrongCategory = -2,
            UnavailableClue = -3,
            NoResponse = int.MinValue, 
        }
    }
}