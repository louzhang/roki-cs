using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Roki.Core.Services.Database.Models;

namespace Roki.Core.Services.Database.Repositories
{
    public interface IGuildRepository : IRepository<Guild>
    {
        Task<Guild> GetOrCreateGuildAsync(SocketGuild guild);
        Task<XpReward> AddXpReward(ulong guildId, int level, string type, string rewardName, string reward);
        Task RemoveXpReward(ulong guildId, string rewardName);
    }
    
    public class GuildRepository : Repository<Guild>, IGuildRepository
    {
        public GuildRepository(DbContext context) : base(context)
        {
        }

        public async Task<Guild> GetOrCreateGuildAsync(SocketGuild guild)
        {
            var gd = await Set.FirstOrDefaultAsync(g => g.GuildId == guild.Id).ConfigureAwait(false);
            if (gd != null) return gd;
            var newGuild = Set.Add(new Guild
            {
                GuildId = guild.Id,
                Name = guild.Name,
                IconId = guild.IconId,
                ChannelCount = guild.Channels.Count,
                MemberCount = guild.MemberCount,
                EmoteCount = guild.Emotes.Count,
                OwnerId = guild.OwnerId,
                RegionId = guild.VoiceRegionId,
                CreatedAt = guild.CreatedAt
            });
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return newGuild.Entity;
        }

        public async Task<XpReward> AddXpReward(ulong guildId, int level, string type, string rewardName, string reward)
        {
            var guild = await Set.FirstAsync(g => g.GuildId == guildId).ConfigureAwait(false);
            var rewardsRaw = guild.XpRewards;

            List<XpReward> rewards;
            int elements;
            
            try
            {
                rewards = JsonSerializer.Deserialize<List<XpReward>>(rewardsRaw);
                elements = rewards.Count;
            }
            catch (JsonException)
            {
                rewards = new List<XpReward>();
                elements = 0;
            }

            var xpReward = new XpReward
            {
                XpLevel = level,
                Type = type,
                RewardName = rewardName,
                Reward = reward
            };
            
            if (elements == 0 || string.IsNullOrWhiteSpace(rewardsRaw))
            {
                var rewardJson = JsonSerializer.Serialize(xpReward);
                guild.XpRewards = $"[{rewardJson}]";
                await Context.SaveChangesAsync().ConfigureAwait(false);
            }
            else
            {
                rewards.Add(xpReward);
                guild.XpRewards = JsonSerializer.Serialize(rewards);
            }
            
            await Context.SaveChangesAsync().ConfigureAwait(false);
            return xpReward;
        }

        public Task RemoveXpReward(ulong guildId, string rewardName)
        {
            throw new System.NotImplementedException();
        }
    }
}