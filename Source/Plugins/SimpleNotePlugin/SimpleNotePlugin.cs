﻿using AlephNote.PluginInterface;
using System;
using System.Net;

namespace AlephNote.Plugins.SimpleNote
{
	public class SimpleNotePlugin : BasicRemotePlugin
	{
		public static readonly Version Version = new Version(0, 0, 0, 1);
		public const string Name = "SimpleNotePlugin";

		private IAlephLogger _logger;

		public SimpleNotePlugin() : base("Simplenote", Version, Guid.Parse("4c73e687-3803-4078-9bf0-554aaafc0873"))
		{
			//
		}

		public override void Init(IAlephLogger logger)
		{
			_logger = logger;
		}

		public override IRemoteStorageConfiguration CreateEmptyRemoteStorageConfiguration()
		{
			return new SimpleNoteConfig();
		}

		public override IRemoteStorageConnection CreateRemoteStorageConnection(IWebProxy proxy, IRemoteStorageConfiguration config)
		{
			return new SimpleNoteConnection(_logger, proxy, (SimpleNoteConfig)config);
		}

		public override INote CreateEmptyNote(IRemoteStorageConfiguration cfg)
		{
			return new SimpleNote(Guid.NewGuid().ToString("N").ToUpper(), (SimpleNoteConfig)cfg);
		}

		public override IRemoteStorageSyncPersistance CreateEmptyRemoteSyncData()
		{
			return new SimpleNoteData();
		}
	}
}
