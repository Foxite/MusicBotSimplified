using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN")!;
ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID")!);
string track = Environment.GetEnvironmentVariable("TRACK")!;

var discord = new DiscordClient(new DiscordConfiguration() {
	Intents = DiscordIntents.Guilds | DiscordIntents.GuildVoiceStates,
	Token = botToken
});

var stopProgram = new CancellationTokenSource();

discord.Ready += (_, _) => {
	Task.Run(async () => {
		try {
			await Task.Delay(TimeSpan.FromSeconds(1));
			DiscordChannel channel = await discord.GetChannelAsync(channelId);

			// no using, this function returns shortly.
			VoiceNextConnection audio = await channel.ConnectAsync();

			int users = 0;

			bool keepTransmitting = true;
			long position = 0;

			void StopTransmitting() {
				keepTransmitting = false;
			}

			byte[] buffer = new byte[4096];

			void StartTransmitting() {
				keepTransmitting = true;
				_ = Task.Run(async () => {
					try {
						await using FileStream file = File.OpenRead(track);
						VoiceTransmitSink transmit = audio.GetTransmitSink();
						while (keepTransmitting) {
							file.Seek(position, SeekOrigin.Begin);
							int count;
							while (keepTransmitting && (count = file.Read(buffer, 0, buffer.Length)) > 0) {
								await transmit.WriteAsync(buffer, 0, count);
							}

							if (keepTransmitting) {
								position = 0;
							} else {
								break;
							}
						}

						position = file.Position;
					} catch (Exception ex) {
						Console.WriteLine(ex);
						stopProgram.Cancel();
					}
				});
			}

			discord.VoiceStateUpdated += (_, e) => {
				if (e.After.Channel != null && e.After.Channel.Id == channelId) {
					users++;
					if (users > 1) {
						StartTransmitting();
					}
				} else if (e.Before.Channel != null && e.Before.Channel.Id == channelId) {
					users--;
					if (users <= 1) {
						StopTransmitting();
					}
				}
				return Task.CompletedTask;
			};

			users = audio.TargetChannel.Users.Count;
			if (users > 1) {
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
