using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Discord;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services;
using Roki.Core.Services.Database;
using Roki.Core.Services.Database.Models;
using Roki.Extensions;
using Roki.Modules.Searches.Common;

namespace Roki.Modules.Searches.Services
{
    public class PokemonService : IRService
    {
        private readonly DbService _db;
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions{PropertyNameCaseInsensitive = true};
        private static readonly Dictionary<string, PokemonData> Data = JsonSerializer.Deserialize<Dictionary<string, PokemonData>>(File.ReadAllText("./data/pokemon.json"), Options);

        public PokemonService(DbService db)
        {
            _db = db;
        }

        public Color GetColorOfPokemon(string color)
        {
            color = color.ToLower();
            switch (color)
            {
                case "black":
                    return new Color(0x000000);
                case "blue":
                    return new Color(0x257CFF);
                case "brown":
                    return new Color(0xA3501A);
                case "gray":
                    return new Color(0x808080);
                case "green":
                    return new Color(0x008000);
                case "pink":
                    return new Color(0xFF65A5);
                case "purple":
                    return new Color(0xA63DE8);
                case "red":
                    return new Color(0xFF3232);
                case "white":
                    return new Color(0xFFFFFF);
                case "yellow":
                    return new Color(0xFFF359);
                default:
                    return new Color(0x000000);
            }
        }

        public async Task<Pokemon> GetPokemonByNameAsync(string query)
        {
            using var uow = _db.GetDbContext();
            query = query.SanitizeStringFull().ToLowerInvariant();
            return await uow.Context.Pokedex.FirstOrDefaultAsync(p => p.Name == query)
                .ConfigureAwait(false);
        }

        public async Task<Pokemon> GetPokemonByIdAsync(int number)
        {
            using var uow = _db.GetDbContext();
            return await uow.Context.Pokedex.Where(p => p.Number == number).OrderBy(p => p.Name).FirstOrDefaultAsync().ConfigureAwait(false);
        }

        public static string GetSprite(string pokemon, int number)
        {
            // temp solution for missing sprites
            string[] sprites;
            if (number > 809 || pokemon.Contains("gmax", StringComparison.OrdinalIgnoreCase) || pokemon.Contains("galar"))
                sprites = Directory.GetFiles("./data/pokemon/gen5", $"{pokemon.Substring(0, 3)}*");
            else
                sprites = Directory.GetFiles("./data/pokemon/ani", $"{pokemon.Substring(0, 3)}*");
            return sprites.First(path => path.Replace("-", "").Replace(".gif", "").Replace(".png", "")
                .EndsWith(pokemon, StringComparison.OrdinalIgnoreCase));
        }

        public string GetEvolution(Pokemon pokemon)
        {
            using var uow = _db.GetDbContext();
            var pkmn = GetBaseEvo(uow, pokemon);
            return pkmn.Evolutions == null
                ? "N/A" 
                : GetEvolutionChain(uow, pkmn);
        }

        private static Pokemon GetBaseEvo(IUnitOfWork uow, Pokemon pokemon)
        {
            if (string.IsNullOrEmpty(pokemon.PreEvolution))
                return pokemon;

            var basic = pokemon;
            do
            { 
                basic = uow.Context.Pokedex.First(p => p.Name == basic.PreEvolution);
            } while (!string.IsNullOrEmpty(basic.PreEvolution));

            return basic;
        }

        private static string GetEvolutionChain(IUnitOfWork uow, Pokemon pokemon)
        {
            if (pokemon.Evolutions == null)
            {
                return pokemon.Species;
            }

            var chain = string.Empty;
            
            chain += $"{pokemon.Species} > {GetEvolutionChain(uow, uow.Context.Pokedex.First(p => p.Name == pokemon.Evolutions.First()))}";
            if (pokemon.Evolutions.Length <= 1) return chain;
            
            foreach (var ev in pokemon.Evolutions.Skip(1))
            {
                var pad = string.Empty;
                if (pokemon.PreEvolution != null)
                    pad += $"{Regex.Replace(pokemon.PreEvolution + pokemon.Species, ".", " ")}   ";
                else
                    pad += Regex.Replace(pokemon.Species, ".", " ");
                chain += $"\n{pad} > {GetEvolutionChain(uow, uow.Context.Pokedex.First(p => p.Name == ev))}";
            }
            
            return chain;
        }

        public async Task<Ability> GetAbilityAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            using var uow = _db.GetDbContext();
            return await uow.Context.Abilities.FirstAsync(a => a.Id == query).ConfigureAwait(false);
        }

        public async Task<Move> GetMoveAsync(string query)
        {
            query = query.SanitizeStringFull().ToLowerInvariant();
            using var uow = _db.GetDbContext();
            return await uow.Context.Moves.FirstAsync(m => m.Id == query).ConfigureAwait(false);
        }
    }
}