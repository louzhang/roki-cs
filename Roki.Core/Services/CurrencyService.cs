using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Roki.Core.Services;
using Roki.Core.Services.Database.Models;

namespace Roki.Services
{
    public interface ICurrencyService : IRService
    {
        Task<bool> ChangeAsync(ulong userId, string reason, long amount, ulong from, ulong to, ulong guildId, ulong channelId, ulong messageId);
        Task<bool> TransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId);
        Task ChangeListAsync(IEnumerable<ulong> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<ulong> from, 
            IEnumerable<ulong> to, IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds);

        long GetCurrency(ulong userId);
    }
    
    public class CurrencyService : ICurrencyService
    {
        private readonly DbService _db;
        private readonly Roki _roki;

        public CurrencyService(DbService db, Roki roki)
        {
            _db = db;
            _roki = roki;
        }
        
        private CurrencyTransaction CreateTransaction(string reason, long amount, ulong from, ulong to, ulong guildId, ulong channelId, ulong messageId) =>
            new CurrencyTransaction
            {
                Amount = amount,
                Reason = reason ?? "-",
                To = to,
                From = from,
                GuildId = guildId,
                ChannelId = channelId,
                MessageId = messageId
            };

        private async Task<bool> InternalChangeAsync(ulong userId, string reason, long amount, ulong from, ulong to, ulong guildId, ulong channelId, 
            ulong messageId)
        {
            using var uow = _db.GetDbContext();
            var success = await uow.Users.UpdateCurrencyAsync(userId, amount).ConfigureAwait(false);
            if (from == _roki.Properties.BotId)
            {
                await uow.Users.UpdateBotCurrencyAsync(from, -amount);
            }
            else if (to == _roki.Properties.BotId)
            {
                await uow.Users.UpdateBotCurrencyAsync(to, -amount);
            }
            if (success)
            {
                var _ = CreateTransaction(reason, amount, from, to, guildId, channelId, messageId);
                uow.Transaction.Add(_);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
            return success;
        }

        private async Task<bool> InternalTransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, 
            ulong messageId)
        {
            using (var uow = _db.GetDbContext())
            {
                var success = await uow.Users.UpdateCurrencyAsync(userIdFrom, -amount).ConfigureAwait(false);
                if (success)
                {
                    await uow.Users.UpdateCurrencyAsync(userIdTo, amount).ConfigureAwait(false);
                    var _ = CreateTransaction(reason, amount, userIdFrom, userIdTo, guildId, channelId, messageId);
                    uow.Transaction.Add(_);
                }

                await uow.SaveChangesAsync().ConfigureAwait(false);
                return success;
            }
        }

        public async Task<bool> ChangeAsync(ulong userId, string reason, long amount, ulong from, ulong to, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalChangeAsync(userId, reason, amount, from, to, guildId, channelId, messageId).ConfigureAwait(false);
        }

        public async Task<bool> TransferAsync(ulong userIdFrom, ulong userIdTo, string reason, long amount, ulong guildId, ulong channelId, ulong messageId)
        {
            return await InternalTransferAsync(userIdFrom, userIdTo, reason, amount, guildId, channelId, messageId);
        }

        public async Task ChangeListAsync(IEnumerable<ulong> users, IEnumerable<string> reasons, IEnumerable<long> amounts, IEnumerable<ulong> from,
            IEnumerable<ulong> to, IEnumerable<ulong> guildIds, IEnumerable<ulong> channelIds, IEnumerable<ulong> messageIds)
        {
            var usersArr = users as ulong[] ?? users.ToArray();
            var reasonsArr = reasons as string[] ?? reasons.ToArray();
            var amountsArr = amounts as long[] ?? amounts.ToArray();
            var fromArr = from as ulong[] ?? from.ToArray();
            var toArr = to as ulong[] ?? to.ToArray();
            var guildsArr = guildIds as ulong[] ?? guildIds.ToArray();
            var chansArr = channelIds as ulong[] ?? channelIds.ToArray();
            var msgsArr = messageIds as ulong[] ?? messageIds.ToArray();
            
            if (usersArr.Length != amountsArr.Length)
                throw new ArgumentException("Cannot perform bulk operation. Arrays are not of equal length");

            using var uow = _db.GetDbContext();
            for (int i = 0; i < usersArr.Length; i++)
            {
                await InternalChangeAsync(usersArr[i], reasonsArr[i], amountsArr[i], fromArr[i], toArr[i], guildsArr[i], chansArr[i],
                    msgsArr[i]).ConfigureAwait(false);
            }

            await uow.SaveChangesAsync().ConfigureAwait(false);
        }

        public long GetCurrency(ulong userId)
        {
            using var uow = _db.GetDbContext();
            return uow.Users.GetUserCurrency(userId);
        }
    }
}