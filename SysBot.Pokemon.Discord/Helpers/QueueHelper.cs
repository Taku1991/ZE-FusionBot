using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.WebSocket;
using PKHeX.Core;
using PKHeX.Core.AutoMod;
using SysBot.Pokemon.Discord.Commands.Bots;
using SysBot.Pokemon.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiscordColor = Discord.Color;

namespace SysBot.Pokemon.Discord;

public static class QueueHelper<T> where T : PKM, new()
{
    private const uint MaxTradeCode = 9999_9999;

    private static readonly Dictionary<int, string> MilestoneImages = new()
    {
        { 1, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/001.png" },
        { 50, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/050.png" },
        { 100, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/100.png" },
        { 150, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/150.png" },
        { 200, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/200.png" },
        { 250, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/250.png" },
        { 300, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/300.png" },
        { 350, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/350.png" },
        { 400, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/400.png" },
        { 450, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/450.png" },
        { 500, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/500.png" },
        { 550, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/550.png" },
        { 600, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/600.png" },
        { 650, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/650.png" },
        { 700, "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/700.png" }
    };

    private static string GetMilestoneDescription(int tradeCount)
    {
        return tradeCount switch
        {
            1 => "Congratulations on your first trade!\n**Status:** Newbie Trainer.",
            50 => "You've reached 50 trades!\n**Status:** Novice Trainer.",
            100 => "You've reached 100 trades!\n**Status:** Pokémon Professor.",
            150 => "You've reached 150 trades!\n**Status:** Pokémon Specialist.",
            200 => "You've reached 200 trades!\n**Status:** Pokémon Champion.",
            250 => "You've reached 250 trades!\n**Status:** Pokémon Hero.",
            300 => "You've reached 300 trades!\n**Status:** Pokémon Elite.",
            350 => "You've reached 350 trades!\n**Status:** Pokémon Trader.",
            400 => "You've reached 400 trades!\n**Status:** Pokémon Sage.",
            450 => "You've reached 450 trades!\n**Status:** Pokémon Legend.",
            500 => "You've reached 500 trades!\n**Status:** Region Master.",
            550 => "You've reached 550 trades!\n**Status:** Trade Master.",
            600 => "You've reached 600 trades!\n**Status:** World Famous.",
            650 => "You've reached 650 trades!\n**Status:** Pokémon Master.",
            700 => "You've reached 700 trades!\n**Status:** Pokémon God.",
            _ => $"Congratulations on reaching {tradeCount} trades! Keep it going!"
        };
    }

    public static async Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, SocketUser trader, bool isBatchTrade = false, int batchTradeNumber = 1, int totalBatchTrades = 1, bool isHiddenTrade = false, bool isMysteryEgg = false, List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        if ((uint)code > MaxTradeCode)
        {
            await context.Channel.SendMessageAsync("Trade code should be 00000000-99999999!").ConfigureAwait(false);
            return;
        }

        try
        {
            // Only send trade code for non-batch trades (batch container will handle its own)
            if (!isBatchTrade)
            {
                if (trade is PB7 && lgcode != null)
                {
                    var (lgfile, lgEmbed) = await SkiaImageHelper.CreateLGCodeSpriteAsync(lgcode).ConfigureAwait(false);
                    await trader.SendFileAsync(lgfile, "Your trade code will be.", embed: lgEmbed).ConfigureAwait(false);
                    await ScheduleFileDeletion(lgfile, 5000).ConfigureAwait(false);
                }
                else
                {
                    await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);
                }
            }

            var result = await AddToTradeQueue(context, trade, code, trainer, sig, routine, type, trader, isBatchTrade, batchTradeNumber, totalBatchTrades, isHiddenTrade, isMysteryEgg, lgcode, ignoreAutoOT, setEdited, isNonNative).ConfigureAwait(false);
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex).ConfigureAwait(false);
        }
    }

    public static Task AddToQueueAsync(SocketCommandContext context, int code, string trainer, RequestSignificance sig, T trade, PokeRoutineType routine, PokeTradeType type, bool ignoreAutoOT = false)
    {
        return AddToQueueAsync(context, code, trainer, sig, trade, routine, type, context.User, ignoreAutoOT: ignoreAutoOT);
    }

    private static async Task<TradeQueueResult> AddToTradeQueue(SocketCommandContext context, T pk, int code, string trainerName,
        RequestSignificance sig, PokeRoutineType type, PokeTradeType t, SocketUser trader, bool isBatchTrade,
        int batchTradeNumber, int totalBatchTrades, bool isHiddenTrade, bool isMysteryEgg = false,
        List<Pictocodes>? lgcode = null, bool ignoreAutoOT = false, bool setEdited = false, bool isNonNative = false)
    {
        // Note: This method should only be called for individual trades now
        // Batch trades use AddBatchContainerToQueueAsync

        var user = trader;
        var userID = user.Id;
        var name = user.Username;
        var trainer = new PokeTradeTrainerInfo(trainerName, userID);
        var notifier = new DiscordTradeNotifier<T>(pk, trainer, code, trader, batchTradeNumber, totalBatchTrades,
            isMysteryEgg, lgcode: lgcode!);

        int uniqueTradeID = GenerateUniqueTradeID();

        var detail = new PokeTradeDetail<T>(pk, trainer, notifier, t, code, sig == RequestSignificance.Favored,
            lgcode, batchTradeNumber, totalBatchTrades, isMysteryEgg, isHiddenTrade, uniqueTradeID, ignoreAutoOT, setEdited);

        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.LinkTrade, name, uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var isSudo = sig == RequestSignificance.Owner;
        var added = Info.AddToTradeQueue(trade, userID, false, isSudo);

        // Start queue position updates for Discord notification
        if (added != QueueResultAdd.AlreadyInQueue && added != QueueResultAdd.NotAllowedItem && notifier is DiscordTradeNotifier<T> discordNotifier)
        {
            // IMPORTANT: Update the notifier's unique trade ID to match the one used in the queue
            // Otherwise the DM will check position with the wrong ID and return incorrect results
            discordNotifier.UpdateUniqueTradeID(uniqueTradeID);
            await discordNotifier.SendInitialQueueUpdate().ConfigureAwait(false);
        }

        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }

        if (added == QueueResultAdd.AlreadyInQueue)
        {
            await context.Channel.SendMessageAsync($"{trader.Mention} - You are already in the queue!").ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        if (added == QueueResultAdd.QueueFull)
        {
            var maxCount = SysCord<T>.Runner.Config.Queues.MaxQueueCount;
            var embed = new EmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("🚫 Queue Full")
                .WithDescription($"The queue is currently full ({maxCount}/{maxCount}). Please try again later when space becomes available.")
                .WithFooter("Queue will open up as trades are completed")
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        if (added == QueueResultAdd.NotAllowedItem)
        {
            var held = pk.HeldItem;
            var itemName = held > 0 ? PKHeX.Core.GameInfo.GetStrings("en").Item[held] : "(none)";
            await context.Channel.SendMessageAsync($"{trader.Mention} - Trade blocked: the held item '{itemName}' cannot be traded in PLZA.").ConfigureAwait(false);
            return new TradeQueueResult(false);
        }

        var embedData = DetailsExtractor<T>.ExtractPokemonDetails(
            pk, trader, isMysteryEgg, type == PokeRoutineType.Clone, type == PokeRoutineType.Dump,
            type == PokeRoutineType.FixOT, type == PokeRoutineType.SeedCheck, false, 1, 1
        );

        try
        {
            (string embedImageUrl, DiscordColor embedColor) = await PrepareEmbedDetails(pk);

            embedData.EmbedImageUrl = isMysteryEgg ? "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/mysteryegg3.png?raw=true&width=300&height=300" :
            type == PokeRoutineType.Dump ? "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/Dumping.png?raw=true&width=300&height=300" :
            type == PokeRoutineType.Clone ? "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/Cloning.png?raw=true&width=300&height=300" :
            type == PokeRoutineType.SeedCheck ? "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/Seeding.png?raw=true&width=300&height=300" :
            type == PokeRoutineType.FixOT ? "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/FixOTing.png?raw=true&width=300&height=300" :
                                       embedImageUrl;

            embedData.HeldItemUrl = string.Empty;
            if (!string.IsNullOrWhiteSpace(embedData.HeldItem))
            {
                string heldItemName = embedData.HeldItem.ToLower().Replace(" ", "");
                embedData.HeldItemUrl = $"https://serebii.net/itemdex/sprites/{heldItemName}.png";
            }

            embedData.IsLocalFile = File.Exists(embedData.EmbedImageUrl);

            var position = Info.CheckPosition(userID, uniqueTradeID, type);
            var botct = Info.Hub.Bots.Count;
            var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;
            var etaMessage = $"Wait Estimate: {baseEta:F1} min(s) for trade.";
            string footerText = $"Current Queue Position: {(position.Position == -1 ? 1 : position.Position)}";
            string trainerMention = trader.Mention;
            string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails, trainerMention);

            if (!string.IsNullOrEmpty(userDetailsText))
            {
                footerText += $"\n{userDetailsText}";
            }
            footerText += $"\n{etaMessage}";
            footerText += $"\nZE FusionBot for Pokémon Hideout {PokeBot.Version}";

            var embedBuilder = new EmbedBuilder()
                .WithColor(embedColor)
                .WithImageUrl(embedData.IsLocalFile ? $"attachment://{Path.GetFileName(embedData.EmbedImageUrl)}" : embedData.EmbedImageUrl)
                .WithFooter(footerText)
                .WithAuthor(new EmbedAuthorBuilder()
                    .WithName(embedData.AuthorName)
                    .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                    .WithUrl("https://hideoutpk.de"));

            DetailsExtractor<T>.AddAdditionalText(embedBuilder);

            if (!isMysteryEgg && type != PokeRoutineType.Clone && type != PokeRoutineType.Dump && type != PokeRoutineType.FixOT && type != PokeRoutineType.SeedCheck)
            {
                DetailsExtractor<T>.AddNormalTradeFields(embedBuilder, embedData, trader.Mention, pk);
            }
            else
            {
                DetailsExtractor<T>.AddSpecialTradeFields(embedBuilder, isMysteryEgg, type == PokeRoutineType.SeedCheck, type == PokeRoutineType.Clone, type == PokeRoutineType.FixOT, trader.Mention);
            }

            // Check if the Pokemon is Non-Native and/or has a Home Tracker
            if (pk is IHomeTrack homeTrack)
            {
                if (homeTrack.HasTracker && isNonNative)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/exclamation.gif";
                    embedBuilder.AddField("**__Notice__**: **This Pokemon is Non-Native & Has Home Tracker.**", "*AutoOT not applied.*");
                }
                else if (homeTrack.HasTracker)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/exclamation.gif";
                    embedBuilder.AddField("**__Notice__**: **Home Tracker Detected.**", "*AutoOT not applied.*");
                }
                else if (isNonNative)
                {
                    embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/exclamation.gif";
                    embedBuilder.AddField("**__Notice__**: **This Pokemon is Non-Native.**", "*Cannot enter HOME & AutoOT not applied.*");
                }
            }
            else if (isNonNative)
            {
                embedBuilder.Footer.IconUrl = "https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/exclamation.gif";
                embedBuilder.AddField("**__Notice__**: **This Pokemon is Non-Native.**", "*Cannot enter HOME & AutoOT not applied.*");
            }

            DetailsExtractor<T>.AddThumbnails(embedBuilder, type == PokeRoutineType.Clone, type == PokeRoutineType.SeedCheck, embedData.HeldItemUrl);

            if (!isHiddenTrade && SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
            {
                var embed = embedBuilder.Build();
                if (embed == null)
                {
                    Console.WriteLine("Error: Embed is null.");
                    await context.Channel.SendMessageAsync("An error occurred while preparing the trade details.");
                    return new TradeQueueResult(false);
                }

                if (embedData.IsLocalFile)
                {
                    await context.Channel.SendFileAsync(embedData.EmbedImageUrl, embed: embed);
                    await ScheduleFileDeletion(embedData.EmbedImageUrl, 0);
                }
                else
                {
                    await context.Channel.SendMessageAsync(embed: embed);
                }
            }
            else
            {
                 var message = $"▹𝗦𝗨𝗖𝗖𝗘𝗦𝗦𝗙𝗨𝗟𝗟𝗬 𝗔𝗗𝗗𝗘𝗗◃\n" +
                 $"//【𝐔𝐒𝐄𝐑: ||Owner Access Only||】\n" +
                 $"//【𝐏𝐎𝐒𝐈𝐓𝐈𝐎𝐍: {position.Position}】\n";

                if (embedData.SpeciesName != "---")
                {
                    message += $"//【𝐏𝐎𝐊𝐄𝐌𝐎𝐍: ||{embedData.SpeciesName}||】\n";
                }

                message += $"//【𝐄𝐓𝐀: {baseEta:F1} Min(s)】";
                await context.Channel.SendMessageAsync(message);
            }
        }
        catch (HttpException ex)
        {
            await HandleDiscordExceptionAsync(context, trader, ex);
            return new TradeQueueResult(false);
        }

        if (SysCord<T>.Runner.Hub.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            int tradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            _ = SendMilestoneEmbed(tradeCount, context.Channel, trader);
        }

        return new TradeQueueResult(true);
    }

    public static async Task AddBatchContainerToQueueAsync(SocketCommandContext context, int code, string trainer, T firstTrade, List<T> allTrades, RequestSignificance sig, SocketUser trader, int totalBatchTrades)
    {
        var userID = trader.Id;
        var name = trader.Username;
        var trainer_info = new PokeTradeTrainerInfo(trainer, userID);
        var notifier = new DiscordTradeNotifier<T>(firstTrade, trainer_info, code, trader, 1, totalBatchTrades, false, lgcode: []);

        int uniqueTradeID = GenerateUniqueTradeID();

        var detail = new PokeTradeDetail<T>(firstTrade, trainer_info, notifier, PokeTradeType.Batch, code,
            sig == RequestSignificance.Favored, null, 1, totalBatchTrades, false)
        {
            BatchTrades = allTrades
        };

        var trade = new TradeEntry<T>(detail, userID, PokeRoutineType.Batch, name, uniqueTradeID: uniqueTradeID);
        var hub = SysCord<T>.Runner.Hub;
        var Info = hub.Queues.Info;
        var added = Info.AddToTradeQueue(trade, userID, false, sig == RequestSignificance.Owner);

        // Send trade code once
        await EmbedHelper.SendTradeCodeEmbedAsync(trader, code).ConfigureAwait(false);

        // Start queue position updates for Discord notification
        if (added != QueueResultAdd.AlreadyInQueue && added != QueueResultAdd.NotAllowedItem && notifier is DiscordTradeNotifier<T> discordNotifier)
        {
            // IMPORTANT: Update the notifier's unique trade ID to match the one used in the queue
            // Otherwise the DM will check position with the wrong ID and return incorrect results
            discordNotifier.UpdateUniqueTradeID(uniqueTradeID);
            await discordNotifier.SendInitialQueueUpdate().ConfigureAwait(false);
        }

        // Handle the display
        if (added == QueueResultAdd.AlreadyInQueue)
        {
            await context.Channel.SendMessageAsync($"{trader.Mention} - You are already in the queue!").ConfigureAwait(false);
            return;
        }

        if (added == QueueResultAdd.QueueFull)
        {
            var maxCount = SysCord<T>.Runner.Config.Queues.MaxQueueCount;
            var embed = new EmbedBuilder()
                .WithColor(DiscordColor.Red)
                .WithTitle("🚫 Queue Full")
                .WithDescription($"The queue is currently full ({maxCount}/{maxCount}). Please try again later when space becomes available.")
                .WithFooter("Queue will open up as trades are completed")
                .WithTimestamp(DateTimeOffset.Now)
                .Build();

            await context.Channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
            return;
        }

        if (added == QueueResultAdd.NotAllowedItem)
        {
            var held = firstTrade.HeldItem;
            var itemName = held > 0 ? PKHeX.Core.GameInfo.GetStrings("en").Item[held] : "(none)";
            await context.Channel.SendMessageAsync($"{trader.Mention} - Trade blocked: the held item '{itemName}' cannot be traded in PLZA.").ConfigureAwait(false);
            return;
        }

        var position = Info.CheckPosition(userID, uniqueTradeID, PokeRoutineType.Batch);
        var botct = Info.Hub.Bots.Count;
        var baseEta = position.Position > botct ? Info.Hub.Config.Queues.EstimateDelay(position.Position, botct) : 0;

        // Get user trade details for footer
        int totalTradeCount = 0;
        TradeCodeStorage.TradeCodeDetails? tradeDetails = null;
        if (SysCord<T>.Runner.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            totalTradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            tradeDetails = tradeCodeStorage.GetTradeDetails(trader.Id);
        }

        // Send initial batch summary message
        await context.Channel.SendMessageAsync($"{trader.Mention} - Added batch trade with {totalBatchTrades} Pokémon to the queue! Position: {position.Position}. Estimated: {baseEta:F1} min(s).").ConfigureAwait(false);

        // Create and send a single combined embed for the entire batch
        if (SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.UseEmbeds)
        {
            try
            {
                // Create combined sprite image (cross-platform via SkiaSharp)
                string spriteImagePath = await SkiaImageHelper.CreateBatchSpriteAsync(allTrades).ConfigureAwait(false);

                // Build Pokemon list for description
                var pokemonList = new System.Text.StringBuilder();
                string maleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.MaleEmoji.EmojiString;
                string femaleEmojiString = SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.FemaleEmoji.EmojiString;

                for (int i = 0; i < allTrades.Count; i++)
                {
                    var pk = allTrades[i];
                    string speciesName = SpeciesName.GetSpeciesName(pk.Species, 2);

                    // Use configured emojis or fallback to unicode symbols
                    string genderSymbol = pk.Gender switch
                    {
                        0 => !string.IsNullOrEmpty(maleEmojiString) ? $" {maleEmojiString}" : " ♂",
                        1 => !string.IsNullOrEmpty(femaleEmojiString) ? $" {femaleEmojiString}" : " ♀",
                        _ => ""
                    };

                    string shinySymbol = pk.IsShiny ? " ✨" : "";
                    pokemonList.AppendLine($"{i + 1}. {speciesName}{genderSymbol}{shinySymbol}");
                }

                // Get OT/TID/SID from first Pokemon
                var firstPk = allTrades[0];
                string otInfo = $"OT: {firstPk.OriginalTrainerName} | TID: {firstPk.DisplayTID} | SID: {firstPk.DisplaySID}";

                // Build footer text
                string trainerMention = trader.Mention;
                string userDetailsText = DetailsExtractor<T>.GetUserDetails(totalTradeCount, tradeDetails, trainerMention);
                string footerText = $"Batch Trade: {totalBatchTrades} Pokémon | Position: {position.Position}";

                if (!string.IsNullOrEmpty(userDetailsText))
                {
                    footerText += $"\n{userDetailsText}";
                }
                footerText += $"\n{otInfo}";
                footerText += $"\nEstimated: {baseEta:F1} min(s) for batch";

                // Create embed
                var embedBuilder = new EmbedBuilder()
                    .WithColor(DiscordColor.Gold)
                    .WithTitle($"🎁 Batch Trade - {trainer}")
                    .WithDescription(pokemonList.ToString())
                    .WithImageUrl($"attachment://{Path.GetFileName(spriteImagePath)}")
                    .WithFooter(footerText)
                    .WithAuthor(new EmbedAuthorBuilder()
                        .WithName($"Trade Request from {trader.Username}")
                        .WithIconUrl(trader.GetAvatarUrl() ?? trader.GetDefaultAvatarUrl())
                        .WithUrl("https://hideoutpk.de"))
                    .WithTimestamp(DateTimeOffset.Now);

                var embed = embedBuilder.Build();

                // Send embed with sprite image
                await context.Channel.SendFileAsync(spriteImagePath, embed: embed);

                // Schedule cleanup of temporary sprite image
                await ScheduleFileDeletion(spriteImagePath, 5000);
            }
            catch (HttpException ex)
            {
                await HandleDiscordExceptionAsync(context, trader, ex);
            }
            catch (Exception ex)
            {
                await context.Channel.SendMessageAsync($"{trader.Mention} - An error occurred while creating the batch trade embed: {ex.Message}");
            }
        }

        // Send milestone embed if applicable
        if (SysCord<T>.Runner.Hub.Config.Trade.TradeConfiguration.StoreTradeCodes)
        {
            var tradeCodeStorage = new TradeCodeStorage();
            int tradeCount = tradeCodeStorage.GetTradeCount(trader.Id);
            _ = SendMilestoneEmbed(tradeCount, context.Channel, trader);
        }
    }

    private static int GenerateUniqueTradeID()
    {
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        int randomValue = Random.Shared.Next(1000);
        return (int)((timestamp % int.MaxValue) * 1000 + randomValue);
    }

    public static async Task<(string, DiscordColor)> PrepareEmbedDetails(T pk)
    {
        try
        {
            return await PrepareEmbedDetailsCore(pk).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"PrepareEmbedDetails fallback: {ex.Message}");
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            string fallbackUrl = TradeExtensions<T>.PokeImg(pk, canGmax, pk.IsEgg, null);
            return (fallbackUrl, new DiscordColor(255, 255, 255));
        }
    }

    private static async Task<(string, DiscordColor)> PrepareEmbedDetailsCore(T pk)
    {
        // Build ball URL
        var strings = GameInfo.GetStrings("en");
        string ballName = strings.balllist[pk.Ball];
        ballName = ballName.Contains("(LA)")
            ? "la" + ballName.Replace(" ", "").Replace("(LA)", "").ToLower()
            : ballName.Replace(" ", "").ToLower();
        string ballImgUrl = $"https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/AltBallImg/20x20/{ballName}.png";

        // Load base species bitmap (egg composite or plain sprite)
        SkiaSharp.SKBitmap? speciesBmp;
        if (pk.IsEgg)
        {
            string eggUrl = GetEggTypeImageUrl(pk);
            string speciesUrl = TradeExtensions<T>.PokeImg(pk, false, true, null);
            speciesBmp = await SkiaImageHelper.CompositeEggWithSpeciesAsync(eggUrl, speciesUrl).ConfigureAwait(false);
        }
        else
        {
            bool canGmax = pk is PK8 pk8 && pk8.CanGigantamax;
            string speciesUrl = TradeExtensions<T>.PokeImg(pk, canGmax, false, SysCord<T>.Runner.Config.Trade.TradeEmbedSettings.PreferredImageSize);
            speciesBmp = await SkiaImageHelper.LoadFromUrlAsync(speciesUrl).ConfigureAwait(false);
        }

        if (speciesBmp == null)
        {
            bool canGmax = pk is PK8 pk8b && pk8b.CanGigantamax;
            return (TradeExtensions<T>.PokeImg(pk, canGmax, pk.IsEgg, null), new DiscordColor(255, 255, 255));
        }

        // Overlay ball (bottom-right corner)
        var withBall = await SkiaImageHelper.OverlayBallAsync(speciesBmp, ballImgUrl).ConfigureAwait(false);
        speciesBmp.Dispose();

        // Compute dominant colour, then save to a local file
        var (r, g, b) = SkiaImageHelper.GetDominantColor(withBall);
        string path = SkiaImageHelper.SaveToFile(withBall);
        withBall.Dispose();

        return (path, new DiscordColor(r, g, b));
    }

    public static async Task ScheduleFileDeletion(string filePath, int delayInMilliseconds)
    {
        await Task.Delay(delayInMilliseconds);
        DeleteFile(filePath);
    }

    private static void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }
    }

    private static async Task SendMilestoneEmbed(int tradeCount, ISocketMessageChannel channel, SocketUser user)
    {
        if (MilestoneImages.TryGetValue(tradeCount, out string? imageUrl))
        {
            var embed = new EmbedBuilder()
                .WithTitle($"{user.Username}'s Milestone Medal")
                .WithDescription(GetMilestoneDescription(tradeCount))
                .WithColor(new DiscordColor(255, 215, 0)) // Gold color
                .WithThumbnailUrl(imageUrl)
                .Build();

            await channel.SendMessageAsync(embed: embed).ConfigureAwait(false);
        }
    }

    private static async Task HandleDiscordExceptionAsync(SocketCommandContext context, SocketUser trader, HttpException ex)
    {
        string message = string.Empty;
        switch (ex.DiscordCode)
        {
            case DiscordErrorCode.InsufficientPermissions or DiscordErrorCode.MissingPermissions:
                {
                    var permissions = context.Guild.CurrentUser.GetPermissions(context.Channel as IGuildChannel);
                    if (!permissions.SendMessages)
                    {
                        message = "You must grant me \"Send Messages\" permissions!";
                        Base.LogUtil.LogError("QueueHelper", message);
                        return;
                    }
                    if (!permissions.ManageMessages)
                    {
                        var app = await context.Client.GetApplicationInfoAsync().ConfigureAwait(false);
                        var owner = app.Owner.Id;
                        message = $"<@{owner}> You must grant me \"Manage Messages\" permissions!";
                    }
                }
                break;

            case DiscordErrorCode.CannotSendMessageToUser:
                {
                    message = context.User == trader ? "You must enable private messages in order to be queued!" : "The mentioned user must enable private messages in order for them to be queued!";
                }
                break;

            default:
                {
                    message = ex.DiscordCode != null ? $"Discord error {(int)ex.DiscordCode}: {ex.Reason}" : $"Http error {(int)ex.HttpCode}: {ex.Message}";
                }
                break;
        }
        await context.Channel.SendMessageAsync(message).ConfigureAwait(false);
    }

    private static string GetEggTypeImageUrl(T pk)
    {
        var pi = pk.PersonalInfo;
        byte typeIndex = pi.Type1;

        string[] typeNames = [
            "Normal", "Fighting", "Flying", "Poison", "Ground", "Rock", "Bug", "Ghost",
            "Steel", "Fire", "Water", "Grass", "Electric", "Psychic", "Ice", "Dragon",
            "Dark", "Fairy"
        ];

        string typeName = (typeIndex >= 0 && typeIndex < typeNames.Length)
            ? typeNames[typeIndex]
            : "Normal";

        return $"https://raw.githubusercontent.com/Secludedly/ZE-FusionBot-Sprite-Images/main/Eggs/Egg_{typeName}.png";
    }

}

