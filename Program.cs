using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;
using IkIheMusicBotSimplified;

string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN")!;
ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID")!);
string track = Environment.GetEnvironmentVariable("TRACK")!;

var discord = new DiscordClient(new DiscordConfiguration() {
	Intents = DiscordIntents.Guilds | DiscordIntents.GuildVoiceStates,
	Token = botToken
});

MusicSessionSource sessionSource = new PcmFileSessionSource(track);
var stopProgram = new CancellationTokenSource();

bool doneReady = false;

discord.Ready += (_, _) => {
	if (doneReady) {
		return Task.CompletedTask;
	}
	
	Task.Run(async () => {
		try {
			doneReady = true;
			await Task.Delay(TimeSpan.FromSeconds(1));
			DiscordChannel channel = await discord.GetChannelAsync(channelId);

			// no using, this function returns shortly, and it causes the bot to disconnect.
			VoiceNextConnection audio = discord.GetVoiceNext().GetConnection(channel.Guild) ?? await channel.ConnectAsync();

			int users = 0;

			bool keepTransmitting = true;

			void StopTransmitting() {
				keepTransmitting = false;
			}

			byte[] buffer = new byte[4096];

			void StartTransmitting() {
				keepTransmitting = true;
				_ = Task.Run(async () => {
					try {
						// Do not dispose, the library doesn't create another one and breaks.
						VoiceTransmitSink transmit = audio.GetTransmitSink();
						using MusicSessionSource.MusicSession session = await sessionSource.GetSession();
						while (keepTransmitting) {
							Stream stream = session.GetStream();
							int count;
							while (keepTransmitting && (count = stream.Read(buffer, 0, buffer.Length)) > 0) {
								await transmit.WriteAsync(buffer, 0, count);
							}
						}
					} catch (Exception ex) {
						Console.WriteLine(ex);
						stopProgram.Cancel();
					} finally {
						// Do not dispose, it causes the bot to disconnect.
						//audio.Dispose();
					}
				});
			}

			discord.VoiceStateUpdated += (_, e) => {
				if ((e.Before == null || e.Before.Channel == null || e.Before.Channel.Id != channelId) && e.After != null && e.After.Channel != null && e.After.Channel.Id == channelId) {
					if (users++ == 0 && users >= 1) {
						StartTransmitting();
					}
				} else if (e.Before != null && e.Before.Channel != null && e.Before.Channel.Id == channelId && (e.After == null || e.After.Channel == null || e.After.Channel.Id != channelId)) {
					if (users-- >= 1 && users == 0) {
						StopTransmitting();
					}
				}
				return Task.CompletedTask;
			};

			users = audio.TargetChannel.Users.Count(member => !member.IsCurrent);
			if (users >= 1) {
				StartTransmitting();
			}
		} catch (Exception ex) {
			Console.WriteLine(ex);
		}
	});
	return Task.CompletedTask;
};

discord.UseVoiceNext();

await discord.ConnectAsync();

await Task.Delay(-1, stopProgram.Token);
