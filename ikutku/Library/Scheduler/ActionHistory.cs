using System;
using clearpixels.Models;

namespace ikutku.Library.Scheduler
{
    public class ActionHistory
    {
        private TimerActionState _timerAction;
        private byte _numberOfTimesExecuted;
        private readonly byte _maxRetries;

        public ActionHistory(byte maxRetries)
        {
            _numberOfTimesExecuted = 0;
            _timerAction = null;
            _maxRetries = maxRetries;
        }

        public void SaveAction(TimerActionState timerAction)
        {

            if (_timerAction == null || !_timerAction.Equals(timerAction))
            {
                _timerAction = timerAction;
                _numberOfTimesExecuted = 0;
            }
            else
            {
                _numberOfTimesExecuted++;
            }
        }
        public void SaveAction(string key, Action action)
        {
            SaveAction(new TimerActionState(key, action, ""));
        }

        public bool IsRetryLimit()
        {
            if (_numberOfTimesExecuted >= _maxRetries)
            {
                return true;
            }

            return false;
        }
    }
}