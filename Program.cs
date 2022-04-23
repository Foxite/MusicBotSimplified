using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Audio;
using Discord.WebSocket;

var discord = new DiscordSocketClient();

ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID"));
string track = Environment.GetEnvironmentVariable("TRACK");

discord.Ready += () => {
	_ = Task.Run(async () => {
		try {
			await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an NRE inside ConnectAsync (yeah)
			var channel = (IVoiceChannel) await discord.GetChannelAsync(channelId);

			using IAudioClient audio = await channel.ConnectAsync(selfDeaf: true);
			await using AudioOutStream transmit = audio.CreatePCMStream(AudioApplication.Music);

			await using FileStream pcm = File.OpenRead(track);
			while (true) {
				pcm.Seek(0, SeekOrigin.Begin);
				await pcm.CopyToAsync(transmit);
			}
		} catch (Exception ex) {
			Console.WriteLine(ex);
		}
	});
	return Task.CompletedTask;
};

discord.Log += lm => {
	Console.WriteLine(lm.ToString(timestampKind: DateTimeKind.Utc));
	return Task.CompletedTask;
};

await discord.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("BOT_TOKEN"));
await discord.StartAsync();

await Task.Delay(-1);
