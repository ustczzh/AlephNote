﻿using CommonNote.PluginInterface;
using CommonNote.Repository;
using CommonNote.Settings;
using Hardcodet.Wpf.TaskbarNotification;
using Microsoft.Win32;
using MSHC.WPF.Extensions.Methods;
using MSHC.WPF.MVVM;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;

namespace CommonNote.WPF.Windows
{
	public class MainWindowViewmodel : ObservableObject, ISynchronizationFeedback
	{
		public ICommand SettingsCommand { get { return new RelayCommand(ShowSettings); } }
		public ICommand CreateNewNoteCommand { get { return new RelayCommand(CreateNote);} }
		public ICommand ResyncCommand { get { return new RelayCommand(Resync); } }
		public ICommand ShowMainWindowCommand { get { return new RelayCommand(ShowMainWindow); } }
		public ICommand ExportCommand { get { return new RelayCommand(ExportNote); } }
		public ICommand DeleteCommand { get { return new RelayCommand(DeleteNote); } }
		public ICommand ExitCommand { get { return new RelayCommand(Exit); } }

		public ICommand ClosingEvent { get { return new RelayCommand<CancelEventArgs>(OnClosing); } }
		public ICommand CloseEvent { get { return new RelayCommand<EventArgs>(OnClose); } }
		public ICommand StateChangedEvent { get { return new RelayCommand<EventArgs>(OnStateChanged); } }

		private AppSettings _settings;
		public AppSettings Settings { get { return _settings; } private set { _settings = value; OnPropertyChanged(); } }

		private NoteRepository _repository;
		public NoteRepository Repository { get { return _repository; } private set { _repository = value; OnPropertyChanged(); OnExplicitPropertyChanged("NotesView"); } }

		private INote _selectedNote;
		public INote SelectedNote { get { return _selectedNote; } private set { _selectedNote = value; OnPropertyChanged(); SelectedNoteChanged(); } }

		private DateTimeOffset? _lastSynchronized = null;

		private string _lastSynchronizedText = "never";
		public string LastSynchronizedText { get { return _lastSynchronizedText; } set { _lastSynchronizedText = value; OnPropertyChanged(); } }

		private string _searchText = string.Empty;
		public string SearchText { get { return _searchText; } private set { _searchText = value; OnPropertyChanged(); FilterNoteList(); } }

		private WindowState _windowState = WindowState.Normal;
		public WindowState WindowState { get { return _windowState; } set { _windowState = value; OnPropertyChanged(); } }

		public ICollectionView NotesView
		{
			get
			{
				if (Repository == null) return CollectionViewSource.GetDefaultView(new List<INote>());

				var source = CollectionViewSource.GetDefaultView(Repository.Notes);
				source.Filter = p => SearchFilter((INote)p);
				return source;
			}
		}

		private bool _preventScintillaFocus = false;
		private bool _forceClose = false;

		public readonly MainWindow Owner;

		public MainWindowViewmodel(AppSettings settings, MainWindow parent)
		{
			_settings = settings;
			Owner = parent;
			_repository = new NoteRepository(App.PATH_LOCALDB, this, settings, settings.NoteProvider, settings.PluginSettings[settings.NoteProvider.GetUniqueID()]);

			Repository.Init();
			Owner.TrayIcon.Visibility = (Settings.CloseToTray || Settings.MinimizeToTray) ? Visibility.Visible : Visibility.Collapsed;

			SelectedNote = Repository.Notes.FirstOrDefault();
		}

		private void ShowSettings()
		{
			var registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

			Settings.LaunchOnBoot = registryKey != null && registryKey.GetValue(App.APPNAME_REG) != null;

			new SettingsWindow(this, Settings).ShowDialog();
		}

		private void CreateNote()
		{
			try
			{
				SelectedNote = Repository.CreateNewNote();
			}
			catch (Exception e)
			{
				MessageBox.Show("Cannot create note cause of " + e.Message + "\r\n\r\n" + e, "CreateNote failed");
			}
		}

		public void ChangeSettings(AppSettings newSettings)
		{
			var reconnectRepo = Settings.NoteProvider != newSettings.NoteProvider || !Settings.PluginSettings[Settings.NoteProvider.GetUniqueID()].IsEqual(newSettings.PluginSettings[newSettings.NoteProvider.GetUniqueID()]);

			if (reconnectRepo)
			{
				_repository.Shutdown();
			}

			Settings = newSettings;
			Settings.Save();

			if (reconnectRepo)
			{
				_repository = new NoteRepository(App.PATH_LOCALDB, this, Settings, Settings.NoteProvider, Settings.PluginSettings[Settings.NoteProvider.GetUniqueID()]);
				_repository.Init();

				OnExplicitPropertyChanged("Repository");

				SelectedNote = Repository.Notes.FirstOrDefault();
			}

			Owner.TrayIcon.Visibility = (Settings.CloseToTray || Settings.MinimizeToTray) ? Visibility.Visible : Visibility.Collapsed;

			if (Settings.LaunchOnBoot)
			{
				var registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
				if (registryKey != null) registryKey.SetValue(App.APPNAME_REG, App.PATH_EXECUTABLE);
			}
			else
			{
				var registryKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
				if (registryKey != null) registryKey.DeleteValue(App.APPNAME_REG);
			}

			Owner.SetupScintilla(Settings);

			SearchText = string.Empty;
		}

		private void SelectedNoteChanged()
		{
			Owner.ResetScintillaScroll();
			if (!_preventScintillaFocus) Owner.FocusScintilla();
		}

		private void Resync()
		{
			Repository.SyncNow();
		}

		public void StartSync()
		{
			LastSynchronizedText = "[SYNCING]";
		}

		public void SyncSuccess(DateTimeOffset now)
		{
			_lastSynchronized = now;
			LastSynchronizedText = now.ToLocalTime().ToString("HH:mm:ss");
		}

		public void SyncError(List<Tuple<string, Exception>> errors)
		{
			Owner.TrayIcon.ShowBalloonTip("Synchronization failed", string.Join(Environment.NewLine, errors.Select(p => p.Item1)), BalloonIcon.Error);

			if (_lastSynchronized != null)
			{
				LastSynchronizedText = _lastSynchronized.Value.ToLocalTime().ToString("HH:mm:ss");
			}
			else
			{
				LastSynchronizedText = "[ERROR]";
			}
		}

		public void ShowMainWindow()
		{
			Owner.Show();
			WindowState = WindowState.Normal;
			Owner.Activate();
			Owner.Focus();
		}

		private void OnClosing(CancelEventArgs e)
		{
			if (Settings.CloseToTray && !_forceClose)
			{
				Owner.Hide();
				e.Cancel = true;
			}
		}

		private void OnClose(EventArgs e)
		{
			Repository.Shutdown();
		}

		private void OnStateChanged(EventArgs e)
		{
			if (WindowState == WindowState.Minimized && Settings.MinimizeToTray)
			{
				Owner.Hide();
			}
		}

		private void ExportNote()
		{
			if (SelectedNote == null) return;

			SaveFileDialog sfd = new SaveFileDialog();
			sfd.Filter = "Text files (*.txt)|*.txt";
			sfd.FileName = SelectedNote.Title + ".txt";

			if (sfd.ShowDialog() == true)
			{
				try
				{
					File.WriteAllText(sfd.FileName, SelectedNote.Text, Encoding.UTF8);
				}
				catch (Exception)
				{
					MessageBox.Show(Owner, "Could not write to file", "Error in WriteAllText");
				}
			}
		}

		private void DeleteNote()
		{
			if (SelectedNote == null) return;

			if (MessageBox.Show(Owner, "Dou you really want to delete this note?", "Delete note ?", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;

			Repository.DeleteNote(SelectedNote, true);

			SelectedNote = Repository.Notes.FirstOrDefault();
		}

		private void Exit()
		{
			_forceClose = true;
			Owner.Close();
		}

		private void FilterNoteList()
		{
			var sn = SelectedNote;

			_preventScintillaFocus = true;
			{
				NotesView.Refresh();
				if (NotesView.Contains(sn)) 
					SelectedNote = sn;
				else
					SelectedNote = NotesView.FirstOrDefault<INote>();
			}
			_preventScintillaFocus = false;
		}

		private bool SearchFilter(INote note)
		{
			if (string.IsNullOrWhiteSpace(SearchText)) return true;

			Regex searchRegex;
			if (IsRegex(SearchText, out searchRegex))
			{
				if (searchRegex.IsMatch(note.Title)) return true;
				if (searchRegex.IsMatch(note.Text)) return true;
				if (note.Tags.Any(t => searchRegex.IsMatch(t))) return true;

				return false;
			}
			else
			{
				if (note.Title.ToLower().Contains(SearchText.ToLower())) return true;
				if (note.Text.ToLower().Contains(SearchText.ToLower())) return true;
				if (note.Tags.Any(t => t.ToLower() == SearchText.ToLower())) return true;

				return false;
			}
		}

		private bool IsRegex(string text, out Regex r)
		{
			try
			{
				if (text.Length >= 3 && text.StartsWith("/") && text.EndsWith("/"))
				{
					r = new Regex(text.Substring(1, text.Length - 2));
					return true;
				}
				else
				{
					r = null;
					return false;
				}

			}
			catch (ArgumentException)
			{
				r = null;
				return false;
			}
		}
	}
}
