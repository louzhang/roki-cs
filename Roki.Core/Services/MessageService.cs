using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using Roki.Core.Services;
using Roki.Core.Services.Database;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public class MessageService : IRService
    {
        private readonly DiscordSocketClient _client;
        private readonly DbService _db;
        private readonly Roki _roki;

        public MessageService(DiscordSocketClient client, DbService db, Roki roki)
        {
            _client = client;
            _db = db;
            _roki = roki;
        }

        public async Task StartService()
        {
            _client.MessageReceived += MessageReceived;
            _client.MessageUpdated += MessageUpdated;
            _client.MessageDeleted += MessageDeleted;
            _client.MessagesBulkDeleted += MessagesBulkDeleted;
            
            await Task.CompletedTask;
        }

        private Task MessageReceived(SocketMessage message)
        {
            if (message.Author.IsBot) return Task.CompletedTask;
            var _ =  Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                var user = await uow.Users.GetUserAsync(message.Author.Id).ConfigureAwait(false);
                var doubleXp = uow.Subscriptions.DoubleXpIsActive(message.Author.Id);
                var fastXp = uow.Subscriptions.FastXpIsActive(message.Author.Id);
                if (fastXp)
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(_roki.Properties.XpFastCooldown))
                        await uow.Users.UpdateXp(user, message, doubleXp).ConfigureAwait(false);
                }
                else
                {
                    if (DateTimeOffset.UtcNow - user.LastXpGain >= TimeSpan.FromMinutes(_roki.Properties.XpCooldown))
                        await uow.Users.UpdateXp(user, message, doubleXp).ConfigureAwait(false);
                }

                string content;
                if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count == 0)
                    content = message.Content;
                else if (!string.IsNullOrWhiteSpace(message.Content) && message.Attachments.Count > 0)
                    content = message.Content + "\n" + string.Join("\n", message.Attachments.Select(a => a.Url));
                else if (message.Attachments.Count > 0)
                    content = string.Join("\n", message.Attachments.Select(a => a.Url));
                else
                    content = "";
                
                uow.Messages.Add(new Message
                {
                    AuthorId = message.Author.Id,
                    Author = message.Author.Username,
                    ChannelId = message.Channel.Id,
                    Channel = message.Channel.Name,
                    GuildId = message.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = message.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = message.Id,
                    Content = content,
                    EditedTimestamp = message.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = message.Timestamp.ToUniversalTime()
                });

                await uow.SaveChangesAsync().ConfigureAwait(false);
            });

            return Task.CompletedTask;
        }

        private Task MessageUpdated(Cacheable<IMessage, ulong> cache, SocketMessage after, ISocketMessageChannel channel)
        {
            if (after.Author.IsBot) return Task.CompletedTask;
            var _ = Task.Run(async () =>
            {
                using var uow = _db.GetDbContext();
                string content;
                if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count == 0)
                    content = after.Content;
                else if (!string.IsNullOrWhiteSpace(after.Content) && after.Attachments.Count > 0)
                    content = after.Content + "\n" + string.Join("\n", after.Attachments.Select(a => a.Url));
                else if (after.Attachments.Count > 0)
                    content = string.Join("\n", after.Attachments.Select(a => a.Url));
                else
                    content = "";
                uow.Messages.Add(new Message
                {
                    AuthorId = after.Author.Id,
                    Author = after.Author.Username,
                    ChannelId = after.Channel.Id,
                    Channel = after.Channel.Name,
                    GuildId = after.Channel is ITextChannel chId ? chId.GuildId : (ulong?) null,
                    Guild = after.Channel is ITextChannel ch ? ch.Guild.Name : null,
                    MessageId = after.Id,
                    Content = content,
                    EditedTimestamp = after.EditedTimestamp?.ToUniversalTime(),
                    Timestamp = after.Timestamp.ToUniversalTime()
                });

                await uow.SaveChangesAsync().ConfigureAwait(false);
            });
                
            return Task.CompletedTask;
        }

        private Task MessageDeleted(Cacheable<IMessage, ulong> cache, ISocketMessageChannel channel)
        {
            if (cache.HasValue && cache.Value.Author.IsBot) return Task.CompletedTask;
            var _ = Task.Run(() =>
            {
                using var uow = _db.GetDbContext();
                uow.Messages.MessageDeleted(cache.Id);
            });
            return Task.CompletedTask;
        }

        private Task MessagesBulkDeleted(IReadOnlyCollection<Cacheable<IMessage, ulong>> caches, ISocketMessageChannel channel)
        {
            var _ = Task.Run(() =>
            {
                using var uow = _db.GetDbContext();
                foreach (var cache in caches)
                {
                    if (cache.HasValue && cache.Value.Author.IsBot) continue;
                    uow.Messages.MessageDeleted(cache.Id);
                }
            });
            
            return Task.CompletedTask;
        }
    }
}