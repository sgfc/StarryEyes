﻿using System;
using System.Runtime.Serialization;

namespace StarryEyes.Breezy.DataModel
{
    [DataContract]
    public class TwitterActivity
    {
        /// <summary>
        /// Internal ID
        /// </summary>
        public long InternalId { get; set; }

        /// <summary>
        /// Time of activity created at
        /// </summary>
        [DataMember]
        public DateTime CreatedAt { get; set; }

        /// <summary>
        /// Activity type
        /// </summary>
        [DataMember]
        public Activity Activity { get; set; }

        /// <summary>
        /// Source user
        /// </summary>
        [DataMember]
        public TwitterUser User { get; set; }

        /// <summary>
        /// Target user
        /// </summary>
        [DataMember]
        public TwitterUser TargetUser { get; set; }

        /// <summary>
        /// Target status
        /// </summary>
        [DataMember]
        public TwitterStatus TargetStatus { get; set; }

        public override string ToString()
        {
            return "[activity]USER: @" + User.ScreenName + " Activity:" + Activity.ToString();
        }
    }

    public enum Activity
    {
        Favorite,
        Unfavorite,
        Retweet,
        Follow,
        Unfollow,
    }
}