using System;

namespace Messages
{
    public class Run { }

    public class ListDirectory
	{
		public ListDirectory(string remotePath)
		{
			this.RemotePath = remotePath;
		}

		public string RemotePath { get; private set; }
	}
}

