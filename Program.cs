using System;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

var discord = new DiscordClient(new DiscordConfiguration() {
	Token = Environment.GetEnvironmentVariable("BOT_TOKEN")
});

VoiceNextExtension voiceNextExtension = discord.UseVoiceNext();

ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID"));
string track = Environment.GetEnvironmentVariable("TRACK");

discord.Ready += (o, e) => {
	_ = Task.Run(async () => {
		try {
			await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an NRE inside ConnectAsync (yeah)
			DiscordChannel channel = await discord.GetChannelAsync(channelId);
			using VoiceNextConnection connection = voiceNextExtension.GetConnection(channel.Guild) ?? await channel.ConnectAsync();
			using VoiceTransmitSink transmit = connection.GetTransmitSink();

			await using FileStream pcm = File.OpenRead(track);
			while (true) {
				pcm.Seek(0, SeekOrigin.Begin);
				await pcm.CopyToAsync(transmit, 256 * transmit.SampleLength);
			}
		} catch (Exception ex) {
			Console.WriteLine(ex);
		}
	});
	
	return Task.CompletedTask;
};

await discord.ConnectAsync();

await Task.Delay(-1);
