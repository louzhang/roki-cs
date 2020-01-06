using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Roki.Common.Attributes;
using Roki.Extensions;
using Roki.Modules.Music.Services;

namespace Roki.Modules.Music
{
    public partial class Music : RokiTopLevelModule<MusicService>
    {
        [RokiCommand, Description, Usage, Aliases]
        [RequireContext(ContextType.Guild)]
        public async Task Queue([Leftover] string query)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            if (string.IsNullOrWhiteSpace(query))
                return;
            
            var user = ctx.User as SocketGuildUser;
            
            await _service.ConnectAsync(user?.VoiceChannel, ctx.Channel as ITextChannel).ConfigureAwait(false);
            await _service.QueueAsync(ctx, query).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Pause()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await _service.PauseAsync(ctx).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Resume()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await _service.ResumeAsync(ctx).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Destroy()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            var user = ctx.User as SocketGuildUser;
            
            await _service.LeaveAsync(user.VoiceChannel).ConfigureAwait(false);
            
            var embed = new EmbedBuilder().WithOkColor()
                .WithDescription($"Disconnected from {user.VoiceChannel.Name}");
            await ctx.Channel.EmbedAsync(embed).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Next()
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            await _service.SkipAsync(ctx).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task ListQueue(int page = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            
            await _service.ListQueueAsync(ctx, page).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task SongRemove(int index = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;

            if (index == 0)
            {
                await ctx.Channel.SendErrorAsync("Please provide the index of the song in the queue.").ConfigureAwait(false);
                return;
            }

            await _service.RemoveSongAsync(ctx, index).ConfigureAwait(false);
        }
        
        [RokiCommand, Description, Usage, Aliases]
        public async Task Volume(int volume)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            if (volume < 0 || volume > 100)
            {
                await ctx.Channel.SendErrorAsync("Volume must be from 0-100.").ConfigureAwait(false);
                return;
            }
            
            await _service.SetVolumeAsync(ctx, (ushort) volume).ConfigureAwait(false);
        }

        [RokiCommand, Description, Usage, Aliases]
        public async Task Seek(int seconds = 0)
        {
            if (!await IsUserInVoice().ConfigureAwait(false))
                return;
            if (seconds == 0)
            {
                await ctx.Channel.SendErrorAsync("Cannot skip that far.").ConfigureAwait(false);
                return;
            }

            await _service.SeekAsync(ctx, seconds).ConfigureAwait(false);
        }

        private async Task<bool> IsUserInVoice()
        {
            var user = ctx.User as SocketGuildUser;
            if (user?.VoiceChannel != null) return true;
            await ctx.Channel.SendErrorAsync("You must be connected to a voice channel.").ConfigureAwait(false);
            return false;
        }
    }
}