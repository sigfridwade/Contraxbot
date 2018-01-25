using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using NadekoBot.Core.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NadekoBot.Common;
using NadekoBot.Common.Attributes;
using Image = ImageSharp.Image;
using ImageSharp;

namespace NadekoBot.Modules.Gambling
{
    public partial class Gambling
    {
        [Group]
        public class FlipCoinCommands : NadekoSubmodule
        {
            private readonly IImageCache _images;
            private readonly IBotConfigProvider _bc;
            private readonly CurrencyService _cs;
            private readonly DbService _db;
            private static readonly NadekoRandom rng = new NadekoRandom();

            public FlipCoinCommands(IDataCache data, CurrencyService cs,
                IBotConfigProvider bc, DbService db)
            {
                _images = data.LocalImages;
                _bc = bc;
                _cs = cs;
                _db = db;
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Flip(int count = 1)
            {
                if (count == 1)
                {
                    if (rng.Next(0, 2) == 1)
                    {
                        using (var heads = _images.Heads.ToStream())
                        {
                            await Context.Channel.SendFileAsync(heads, "heads.jpg", Context.User.Mention + " " + GetText("flipped", Format.Bold(GetText("heads")))).ConfigureAwait(false);
                        }
                    }
                    else
                    {
                        using (var tails = _images.Tails.ToStream())
                        {
                            await Context.Channel.SendFileAsync(tails, "tails.jpg", Context.User.Mention + " " + GetText("flipped", Format.Bold(GetText("tails")))).ConfigureAwait(false);
                        }
                    }
                    return;
                }
                if (count > 10 || count < 1)
                {
                    await ReplyErrorLocalized("flip_invalid", 10).ConfigureAwait(false);
                    return;
                }
                var imgs = new Image<Rgba32>[count];
                for (var i = 0; i < count; i++)
                {
                    using (var heads = _images.Heads.ToStream())
                    using (var tails = _images.Tails.ToStream())
                    {
                        if (rng.Next(0, 10) < 5)
                        {
                            imgs[i] = Image.Load(heads);
                        }
                        else
                        {
                            imgs[i] = Image.Load(tails);
                        }
                    }
                }
                await Context.Channel.SendFileAsync(imgs.Merge().ToStream(), $"{count} coins.png").ConfigureAwait(false);
            }

            public enum BetFlipGuess
            {
                H = 1,
                Head = 1,
                Heads = 1,
                T = 2,
                Tail = 2,
                Tails = 2
            }

            [NadekoCommand, Usage, Description, Aliases]
            public Task Betflip(Allin _, BetFlipGuess guess)
            {
                long cur;
                using (var uow = _db.UnitOfWork)
                {
                    cur = uow.DiscordUsers.GetUserCurrency(Context.User.Id);
                }
                return Betflip(cur, guess);
            }

            [NadekoCommand, Usage, Description, Aliases]
            public async Task Betflip(long amount, BetFlipGuess guess)
            {
                if (amount < _bc.BotConfig.MinimumBetAmount)
                {
                    await ReplyErrorLocalized("min_bet_limit", _bc.BotConfig.MinimumBetAmount + _bc.BotConfig.CurrencySign).ConfigureAwait(false);
                    return;
                }
                var removed = await _cs.RemoveAsync(Context.User, "Betflip Gamble", amount, false, gamble: true).ConfigureAwait(false);
                if (!removed)
                {
                    await ReplyErrorLocalized("not_enough", _bc.BotConfig.CurrencyPluralName).ConfigureAwait(false);
                    return;
                }
                BetFlipGuess result;
                IEnumerable<byte> imageToSend;
                if (rng.Next(0, 2) == 1)
                {
                    imageToSend = _images.Heads;
                    result = BetFlipGuess.Heads;
                }
                else
                {
                    imageToSend = _images.Tails;
                    result = BetFlipGuess.Tails;
                }

                string str;
                if (guess == result)
                { 
                    var toWin = (int)Math.Round(amount * _bc.BotConfig.BetflipMultiplier);
                    str = Context.User.Mention + " " + GetText("flip_guess", toWin + _bc.BotConfig.CurrencySign);
                    await _cs.AddAsync(Context.User, "Betflip Gamble", toWin, false, gamble:true).ConfigureAwait(false);
                }
                else
                {
                    str = Context.User.Mention + " " + GetText("better_luck");
                }
                using (var toSend = imageToSend.ToStream())
                {
                    await Context.Channel.SendFileAsync(toSend, "result.png", str).ConfigureAwait(false);
                }
            }
        }
    }
}