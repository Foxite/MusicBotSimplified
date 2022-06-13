using System;
using System.IO;
using System.Linq;
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

bool doneReady = false;

discord.Ready += (_, _) => {
	if (doneReady) {
		return Task.CompletedTask;
	}
	
	Task.Run(async () => {
		try {
			doneReady = true;
			Console.WriteLine("Begin ready callback.");
			await Task.Delay(TimeSpan.FromSeconds(1));
			DiscordChannel channel = await discord.GetChannelAsync(channelId);

			// no using, this function returns shortly.
			VoiceNextConnection audio = discord.GetVoiceNext().GetConnection(channel.Guild) ?? await channel.ConnectAsync();

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
						Console.WriteLine("Start transmitting.");
						await using FileStream file = File.OpenRead(track);
						VoiceTransmitSink transmit = audio.GetTransmitSink();
						while (keepTransmitting) {
							file.Seek(position, SeekOrigin.Begin);
							int count = -2;
							Console.WriteLine("Begin playback loop.");
							while (keepTransmitting && (count = file.Read(buffer, 0, buffer.Length)) > 0) {
								await transmit.WriteAsync(buffer, 0, count);
							}
							Console.WriteLine($"End playback loop. keepTransmitting: {keepTransmitting}, count: {count}");

							if (keepTransmitting) {
								position = 0;
							} else {
								break;
							}
						}
						
						position = file.Position;
						Console.WriteLine($"Stop transmitting. Position: {position}");
					} catch (Exception ex) {
						Console.WriteLine(ex);
						stopProgram.Cancel();
					}
				});
			}

			discord.VoiceStateUpdated += (_, e) => {
				Console.WriteLine($"Voice state updated. users now: {users}");
				if ((e.Before == null || e.Before.Channel == null || e.Before.Channel.Id != channelId) && e.After != null && e.After.Channel != null && e.After.Channel.Id == channelId) {
					Console.WriteLine("User appears to have joined");
					if (users++ == 0 && users >= 1) {
						Console.WriteLine("Call startTransmitting");
						StartTransmitting();
					}
				} else if (e.Before != null && e.Before.Channel != null && e.Before.Channel.Id == channelId && (e.After == null || e.After.Channel == null || e.After.Channel.Id != channelId)) {
					Console.WriteLine("User appears to have left");
					if (users-- >= 1 && users == 0) {
						Console.WriteLine("Call stopTransmitting");
						StopTransmitting();
					}
				}
				Console.WriteLine($"End of VSU. Users now: {users}");
				return Task.CompletedTask;
			};

			users = audio.TargetChannel.Users.Count(member => !member.IsCurrent);
			Console.WriteLine($"End of ready callback. Current users: {users}");
			if (users >= 1) {
				Console.WriteLine("Call startTransmitting at end of ready callback");
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
