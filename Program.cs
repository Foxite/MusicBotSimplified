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
		try {
			var channel = (IVoiceChannel) await discord.GetChannelAsync(channelId);

			using IAudioClient audio = await channel.ConnectAsync(selfDeaf: true);
			await using AudioOutStream transmit = audio.CreatePCMStream(AudioApplication.Music);

			while (true) {
				await using FileStream pcm = File.OpenRead(track);
				await pcm.CopyToAsync(transmit);
				Console.WriteLine("Loop end");
			}
		} catch (Exception ex) {
			Console.WriteLine(ex);
		} finally {
			stopProgram.Cancel();
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
