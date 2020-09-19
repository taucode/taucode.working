﻿using System;
using System.Collections.Generic;
using System.Linq;
using TauCode.Working.Jobs;

namespace TauCode.Working.Schedules
{
    public class ConcreteSchedule : ISchedule
    {
        private readonly List<DateTimeOffset?> _dueTimes;

        public ConcreteSchedule(params DateTimeOffset[] dueTimes)
        {
            _dueTimes = dueTimes
                .Distinct()
                .Cast<DateTimeOffset?>()
                .ToList();
        }

        public string Description { get; set; }

        public DateTimeOffset GetDueTimeAfter(DateTimeOffset after)
        {
            var dueTime = _dueTimes.FirstOrDefault(x => x.Value >= after) ?? JobExtensions.Never;
            return dueTime;
        }
    }
}
