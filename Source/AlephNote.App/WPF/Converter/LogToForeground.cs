﻿using AlephNote.Log;
using MSHC.WPF.MVVM;
using System;
using System.Windows.Media;

namespace AlephNote.WPF.Converter
{
	class LogToForeground : OneWayConverter<LogEventType, Brush>
	{
		public LogToForeground() { }

		protected override Brush Convert(LogEventType value, object parameter)
		{
			switch (value)
			{
				case LogEventType.Debug: return Brushes.DimGray;
				case LogEventType.Information: return Brushes.Black;
				case LogEventType.Warning: return Brushes.Black;
				case LogEventType.Error: return Brushes.Black;

				default:
					throw new ArgumentOutOfRangeException("value");
			}
		}
	}
}
