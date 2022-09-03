using System;
using System.IO;
using System.Threading.Tasks;

namespace IkIheMusicBotSimplified; 

public abstract class MusicSessionSource {
	public abstract Task<MusicSession> GetSession();
	
	public abstract class MusicSession : IDisposable {
		public abstract Stream GetStream();
		public virtual void Dispose() { }
	}
}
