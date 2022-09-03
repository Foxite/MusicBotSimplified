using System;
using System.IO;
using System.Threading.Tasks;

namespace IkIheMusicBotSimplified; 

public class PcmFileSessionSource : MusicSessionSource {
	private readonly string m_Path;
	private long m_Position;

	public PcmFileSessionSource(string path) {
		m_Path = path;
	}

	public override Task<MusicSession> GetSession() {
		FileStream file = File.OpenRead(m_Path);
		
		return Task.FromResult((MusicSession) new PcmMusicSession(file, this));
	}

	public class PcmMusicSession : MusicSession {
		private readonly Stream m_Stream;
		private readonly PcmFileSessionSource m_Source;

		public PcmMusicSession(Stream stream, PcmFileSessionSource source) {
			m_Stream = stream;
			m_Source = source;
		}

		public override Stream GetStream() {
			if (m_Stream.Position >= m_Stream.Length || m_Stream.Position < 0) {
				m_Source.m_Position = 0;
			}
			m_Stream.Seek(m_Source.m_Position, SeekOrigin.Begin);
			return m_Stream;
		}

		public override void Dispose() {
			m_Source.m_Position = m_Stream.Position;
			m_Stream.Dispose();
		}
	}
}
