using System;
using System.IO;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.VoiceNext;

var discord = new DiscordClient(new DiscordConfiguration() {
	Token = Environment.GetEnvironmentVariable("BOT_TOKEN")
});

VoiceNextExtension voicenextExtension = discord.UseVoiceNext();

ulong channelId = ulong.Parse(Environment.GetEnvironmentVariable("CHANNEL_ID"));
string track = Environment.GetEnvironmentVariable("TRACK");

discord.Ready += (o, e) => {
	_ = Task.Run(async () => {
		DiscordChannel channel = await discord.GetChannelAsync(channelId);
		while (true) {
			await Task.Delay(TimeSpan.FromSeconds(1)); // prevents an error (idk)
			using VoiceNextConnection connection = voicenextExtension.GetConnection(channel.Guild) ?? await channel.ConnectAsync();
			await using Stream pcm = File.OpenRead(track);
			using VoiceTransmitSink transmit = connection.GetTransmitSink();
			while (true) {
				await pcm.CopyToAsync(transmit, 256 * transmit.SampleLength);
				pcm.Position = 0;
			}
		}
	});
	
	return Task.CompletedTask;
};

await discord.ConnectAsync();

await Task.Delay(-1);
