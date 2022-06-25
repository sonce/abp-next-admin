﻿using LINGYUN.Abp.Notifications;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace LINGYUN.Abp.MessageService.Notifications
{
    public class UserNotificationChangeReadStateDto
    {
        [Required]
        [DisplayName("Notifications:Id")]
        public long NotificationId { get; set; }

        [Required]
        [DisplayName("Notifications:State")]
        public NotificationReadState ReadState { get; set; } = NotificationReadState.Read;
    }
}
