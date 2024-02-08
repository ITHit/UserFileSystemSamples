using System;
namespace Common.Core
{
	public class NotificationAnotherDomainIsRegistered: NotificationItemSettings
    {
		public string CurrentDomain { get; set; } = string.Empty;
	}
}

