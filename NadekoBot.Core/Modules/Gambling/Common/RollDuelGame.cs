﻿using NadekoBot.Common;
using NadekoBot.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NadekoBot.Core.Modules.Gambling.Common
{
    public class RollDuelGame
    {
        public ulong P1 { get; }
        public ulong P2 { get; }

        private readonly ulong _botId;

        public long Amount { get; }

        private readonly CurrencyService _cs;

        public enum State
        {
            Waiting,
            Running,
            Ended,
        }

        public enum Reason
        {
            Normal,
            NoFunds,
            Timeout,
        }
        
        private readonly Timer _timeoutTimer;
        private readonly NadekoRandom _rng = new NadekoRandom();
        private readonly SemaphoreSlim _locker = new SemaphoreSlim(1, 1);
        
        public event Func<RollDuelGame, Task> OnGameTick;
        public event Func<RollDuelGame, Reason, Task> OnEnded;

        public List<(int, int)> Rolls { get; } = new List<(int, int)>();
        public State CurrentState { get; private set; }
        public ulong Winner { get; private set; }

        public RollDuelGame(CurrencyService cs, ulong botId, ulong p1, ulong p2, long amount)
        {
            this.P1 = p1;
            this.P2 = p2;
            this._botId = botId;
            this.Amount = amount;
            _cs = cs;

            _timeoutTimer = new Timer(async delegate
            {
                await _locker.WaitAsync();
                try
                {
                    if (CurrentState != State.Waiting)
                        return;
                    CurrentState = State.Ended;
                    await OnEnded?.Invoke(this, Reason.Timeout);
                }
                catch { }
                finally
                {
                    _locker.Release();
                }
            }, null, TimeSpan.FromSeconds(15), TimeSpan.FromMilliseconds(-1));
        }

        public async Task StartGame()
        {
            await _locker.WaitAsync().ConfigureAwait(false);
            try
            {
                if (CurrentState != State.Waiting)
                    return;
                _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
                CurrentState = State.Running;
            }
            finally
            {
                _locker.Release();
            }

            if(!_cs.Remove(P1, "Roll Duel", Amount))
            {
                await OnEnded?.Invoke(this, Reason.NoFunds);
                CurrentState = State.Ended;
                return;
            }
            if(!_cs.Remove(P2, "Roll Duel", Amount))
            {
                await _cs.AddAsync(P1, "Roll Duel - refund", Amount);
                await OnEnded?.Invoke(this, Reason.NoFunds);
                CurrentState = State.Ended;
                return;
            }

            int n1, n2;
            do
            {
                n1 = _rng.Next(0, 5);
                n2 = _rng.Next(0, 5);
                Rolls.Add((n1, n2));
                if (n1 != n2)
                {
                    if (n1 > n2)
                    {
                        Winner = P1;                                                                                                                                                                                                                                                                                                
                    }
                    else
                    {
                        Winner = P2;
                    }
                    var won = (long)(Amount * 2 * 0.98f);
                    await _cs.AddAsync(Winner, "Roll Duel win", won)
                        .ConfigureAwait(false);

                    await _cs.AddAsync(_botId, "Roll Duel fee", Amount * 2 - won)
                        .ConfigureAwait(false);
                }
                try { await OnGameTick?.Invoke(this); } catch { }
                await Task.Delay(2500).ConfigureAwait(false);
                if (n1 != n2)
                    break;
            }
            while (true);
            CurrentState = State.Ended;
            await OnEnded?.Invoke(this, Reason.Normal);
        }
    }

    public struct RollDuelChallenge
    {
        public ulong Player1 { get; set; }
        public ulong Player2 { get; set; }
    }
}
