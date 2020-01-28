using System;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using NLog;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Services;

namespace Roki.Modules.Help.Services
{
    public class HelpService : IRokiService
    {
        private readonly CommandHandler _command;
        private readonly Logger _log;

        public HelpService(CommandHandler command)
        {
            _command = command;
            _log = LogManager.GetCurrentClassLogger();
        }

        public Task LateExecute(DiscordSocketClient client, IGuild guild, IUserMessage msg)
        {
            try
            {
                if (guild == null)
                {
                }
            }
            catch (Exception e)
            {
                _log.Warn(e);
            }

            return Task.CompletedTask;
        }

        public EmbedBuilder GetCommandHelp(CommandInfo info, IGuild guild)
        {
            var prefix = _command.DefaultPrefix;

            var str = $"**`{prefix + info.Aliases.First()}`**";
            var aliases = string.Join("/", info.Aliases.Skip(1).Select(a => $"`{a}`"));
            if (!string.IsNullOrWhiteSpace(aliases)) str += $"/{aliases}";
            var embed = new EmbedBuilder().AddField(str, info.RealSummary(prefix), true);

            var reqs = GetCommandRequirements(info);
            if (reqs.Any()) embed.AddField("Requires", string.Join("\n", reqs));

            embed.WithOkColor()
                .AddField("Usage", info.RealRemarks(prefix), true)
                .WithFooter($"Module: {info.Module.GetTopLevelModule().Name}");

            var options = ((RokiOptions) info.Attributes.FirstOrDefault(x => x is RokiOptions))?.OptionType;
            if (options != null)
            {
                var helpString = GetCommandOptionHelp(options);
                if (!string.IsNullOrWhiteSpace(helpString))
                    embed.AddField("Options", helpString);
            }

            return embed;
        }

        private static string GetCommandOptionHelp(Type option)
        {
            var str = option.GetProperties()
                .Select(x => x.GetCustomAttributes(true).FirstOrDefault(a => a is OptionAttribute))
                .Where(x => x != null)
                .Cast<OptionAttribute>()
                .Select(x =>
                {
                    var toReturn = $"`--{x.LongName}`";

                    if (!string.IsNullOrWhiteSpace(x.ShortName))
                        toReturn += $" (`-{x.ShortName}`)";

                    toReturn += $"   {x.HelpText}  ";
                    return toReturn;
                });
            return string.Join("\n", str);
        }

        private static string[] GetCommandRequirements(CommandInfo info)
        {
            return info.Preconditions
                // TODO add owner only attribute here
                .Where(att => att is RequireUserPermissionAttribute)
                .Select(att =>
                {
                    var perm = (RequireUserPermissionAttribute) att;
                    if (perm.GuildPermission != null)
                        return (perm.GuildPermission + " Server Permission")
                            .Replace("Guild", "Server", StringComparison.InvariantCulture);

                    return (perm.ChannelPermission + " Channel Permission")
                        .Replace("Guild", "Server", StringComparison.InvariantCulture);
                })
                .ToArray();
        }
    }
}