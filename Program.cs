using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

string botToken = Environment.GetEnvironmentVariable("BOT_TOKEN")!;
ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID")!);
string track = Environment.GetEnvironmentVariable("TRACK")!;

var discord = new DiscordSocketClient(new DiscordSocketConfig() {
	GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildVoiceStates
});

var stopProgram = new CancellationTokenSource();

discord.Ready += () => {
	_ = Task.Run(async () => {
		var channel = (IVoiceChannel) await discord.GetChannelAsync(channelId);

		// no using, this function returns shortly.
		IAudioClient audio = await channel.ConnectAsync(selfDeaf: true);

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
					await using AudioOutStream transmit = audio.CreatePCMStream(AudioApplication.Music);
					while (keepTransmitting) {
						file.Seek(position, SeekOrigin.Begin);
						int count;
						while (keepTransmitting && (count = file.Read(buffer, 0, buffer.Length)) > 0) {
							transmit.Write(buffer, 0, count);
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

		discord.UserVoiceStateUpdated += (user, before, after) => {
			if (after.VoiceChannel != null && after.VoiceChannel.Id == channelId) {
				if (users == 0) {
					StartTransmitting();
				}
				users++;
			} else if (before.VoiceChannel != null && before.VoiceChannel.Id == channelId) {
				users--;
				if (users == 0) {
					StopTransmitting();
				}
			}
			return Task.CompletedTask;
		};
		
		users = audio.GetStreams().Count;
		if (users > 0) {
			StartTransmitting();
		}
	});
	return Task.CompletedTask;
};

discord.Log += lm => {
	Console.WriteLine(lm.ToString(timestampKind: DateTimeKind.Utc));
	return Task.CompletedTask;
};

await discord.LoginAsync(TokenType.Bot, botToken);
await discord.StartAsync();

await Task.Delay(-1, stopProgram.Token);
