using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using ikutku.Models.twitter;

namespace ikutku.Models.json
{
    public class ApiError
    {
        private readonly ConcurrentDictionary<TwitterErrorCode, dynamic> _errors;

        public ApiError()
        {
            _errors = new ConcurrentDictionary<TwitterErrorCode, dynamic>();
        }

        public void Add(TwitterErrorCode code, dynamic obj = null)
        {
            switch (code)
            {
                case TwitterErrorCode.ACCESS_DENIED:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Twitter is not allowing you to do that.");
                    }
                    break;
                case TwitterErrorCode.NO_REPLY:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Twitter is not responding. Please try again in awhile.");
                    }
                    break;
                case TwitterErrorCode.ACCOUNT_SUSPENDED:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Your twitter account has been suspended");
                    }
                    break;
                case TwitterErrorCode.ACCOUNT_SUSPENDED2:
                case TwitterErrorCode.FOLLOW_BLOCKED:
                    {
                        dynamic list;
                        if (_errors.TryGetValue(code, out list))
                        {
                            list.Add(obj);
                            _errors[code] = list;
                        }
                        else
                        {
                            list = new List<string> { obj };
                            _errors.TryAdd(code, list);
                        }
                    }
                    break;
                case TwitterErrorCode.OVERCAPACITY:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Twitter is over capacity. Try again in a short while.");
                    }
                    break;
                case TwitterErrorCode.INTERNAL_ERROR:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Twitter returned an error. Please try again.");
                    }
                    break;
                case TwitterErrorCode.FAIL_AUTHENTICATION:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Twitter could not authenticate you. Please try again later or sign out and sign in again.");
                    }
                    break;
                case TwitterErrorCode.FOLLOW_RATE_LIMIT_EXCEEDED:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Follow limit hit for today.");
                    }
                    break;
                case TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Your credentials are invalid. Please sign out and sign in again.");
                    }
                    break;
                case TwitterErrorCode.UNKNOWN_ERROR:
                    if (!_errors.ContainsKey(code))
                    {
                        _errors.TryAdd(code, "Unknown error. Please try again.");
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(code.ToString());
            }
        }

        public override string ToString()
        {
            if (_errors.ContainsKey(TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS))
            {
                return _errors[TwitterErrorCode.INVALID_OR_EXPIRED_CREDENTIALS];
            }

            if (_errors.ContainsKey(TwitterErrorCode.ACCESS_DENIED))
            {
                return _errors[TwitterErrorCode.ACCESS_DENIED];
            }

            if (_errors.ContainsKey(TwitterErrorCode.ACCOUNT_SUSPENDED))
            {
                return _errors[TwitterErrorCode.ACCOUNT_SUSPENDED];
            }

            if (_errors.ContainsKey(TwitterErrorCode.NO_REPLY))
            {
                return _errors[TwitterErrorCode.NO_REPLY];
            }

            if (_errors.ContainsKey(TwitterErrorCode.ACCOUNT_SUSPENDED2))
            {
                return string.Format("Unable to follow suspended twitter accounts => {0}",
                                     string.Join(", ", _errors[TwitterErrorCode.ACCOUNT_SUSPENDED2]));
            }

            if (_errors.ContainsKey(TwitterErrorCode.FOLLOW_BLOCKED))
            {
                return string.Format("These users have blocked you fom following them => {0}",
                                     string.Join(", ", _errors[TwitterErrorCode.FOLLOW_BLOCKED]));
            }

            if (_errors.ContainsKey(TwitterErrorCode.FOLLOW_RATE_LIMIT_EXCEEDED))
            {
                return _errors[TwitterErrorCode.FOLLOW_RATE_LIMIT_EXCEEDED];
            }

            if (_errors.ContainsKey(TwitterErrorCode.OVERCAPACITY))
            {
                return _errors[TwitterErrorCode.OVERCAPACITY];
            }

            if (_errors.ContainsKey(TwitterErrorCode.FAIL_AUTHENTICATION))
            {
                return _errors[TwitterErrorCode.FAIL_AUTHENTICATION];
            }
            
            if (_errors.ContainsKey(TwitterErrorCode.INTERNAL_ERROR))
            {
                return _errors[TwitterErrorCode.INTERNAL_ERROR];
            }

            if (_errors.Count != 0)
            {
                return _errors.Values.First();
            }

            return "";
        }

    }
}
