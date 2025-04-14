using System;
using System.Diagnostics.Contracts;
using System.Numerics;
using ImGuiNET;
using Newtonsoft.Json;

namespace April.Config.Editor;

public class MainWindow(ConfigService configService, ImageLoader imageLoader)
{
    private string path =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "gacha_config.json");

    public void Show()
    {
        ImGui.SetNextWindowSize(new Vector2(800, 600), ImGuiCond.FirstUseEver);
        ImGui.Begin(
#if DEBUG
            "(DEBUG BUILD) Incredibly dumb and over-engineered " +
#endif
            "Config Editor");

        if (ImGui.Button($"{Codicons.Plus} New"))
        {
            configService.NewConfig();
        }

        ImGui.InputTextWithHint("##ConfigFilePath", "Path to config...", ref path, Utility.IMGUI_DEFAULT_MAX_LENGTH);

        ImGui.BeginDisabled(!Directory.Exists(Path.GetDirectoryName(path)) || Path.EndsInDirectorySeparator(path) ||
                            Directory.Exists(path));

        ImGui.SameLine();

        ImGui.BeginDisabled(configService.ConfigFile == null);
        if (ImGui.Button($"{Codicons.Save} Save"))
        {
            configService.SaveConfigToFile(path);
        }

        ImGui.EndDisabled();

        ImGui.SameLine();

        ImGui.BeginDisabled(!File.Exists(path));
        if (ImGui.Button($"{Codicons.File} Load"))
        {
            configService.LoadConfigFromFile(path);
        }

#if DEBUG
        ImGui.SameLine();

        if (ImGui.Button($"{Codicons.Warning} Delete"))
        {
            File.Delete(path);
            configService.ClearConfig();
        }
#endif

        ImGui.EndDisabled();
        ImGui.EndDisabled();

        if (configService.ConfigFile == null)
        {
            ImGui.End();
            return;
        }

        ImGui.InputInt("Roll cooldown (seconds)", ref configService.ConfigFile.cooldownTime);
        configService.ConfigFile.cooldownTime = Math.Max(configService.ConfigFile.cooldownTime, 0);
        ImGui.Spacing();
        ImGui.InputInt("Roll command cost", ref configService.ConfigFile.rollCommandCost);
        ImGui.Spacing();
        ImGui.InputText("Coin emote", ref configService.ConfigFile.coinEmote, Utility.IMGUI_DEFAULT_MAX_LENGTH);
        var parseSuccess = Utility.TryParseEmote(configService.ConfigFile.coinEmote, out var emote);
        if (!parseSuccess)
        {
            ImGui.TextColored(new Vector4(1f, 0f, 0f, 1f), "Not a valid emote!");
        }

        ImGui.InputText("Coin name", ref configService.ConfigFile.coinName, Utility.IMGUI_DEFAULT_MAX_LENGTH);

        ImGui.Spacing();

        if (parseSuccess)
        {
            const int maxHeight = 20;
            ImGui.Text($"You have");
            ImGui.SameLine();
            ImageFromUrl($"https://cdn.discordapp.com/emojis/{emote!.id}.webp?size=128", maxHeight);
            ImGui.SameLine();
            ImGui.Text($"400 {configService.ConfigFile.coinName}.");
        }

        ImGui.Spacing();

        var vec3Col = Utility.IntToRGB((int)configService.ConfigFile.defaultEmbedColor);
        ImGui.ColorEdit3("Default embed color", ref vec3Col);
        configService.ConfigFile.defaultEmbedColor = (uint)Utility.RGBToInt(vec3Col.X, vec3Col.Y, vec3Col.Z);

        if (ImGui.CollapsingHeader("Boxes"))
        {
            {
                var boxes = configService.ConfigFile.boxes;
                var selectedBox = boxes.FirstOrDefault(x => x.Guid == configService.ConfigFile.rollCommandDefaultBox);
                var index = selectedBox != null ? boxes.IndexOf(selectedBox) + 1 : 0;
                var comboBoxItems = boxes.Select(x => $"{x.name} ({x.Guid})").Prepend("").ToArray();

                ImGui.Combo("Default box for roll command", ref index, comboBoxItems, comboBoxItems.Length);

                configService.ConfigFile.rollCommandDefaultBox = index == 0 ? null : boxes[index - 1].Guid;

                ImGui.Separator();
            }

            Utility.ArrayDisplay(configService.ConfigFile.boxes,
                (box, boxRemoveButton) =>
                {
                    boxRemoveButton($"##{box.Guid}");
                    ImGui.SameLine();
                    if (!ImGui.TreeNode($"{box.Guid}", $"{box.name} ({box.Guid})")) return;
                    ImGui.InputText("Name", ref box.name, Utility.IMGUI_DEFAULT_MAX_LENGTH);
                    ImGui.Separator();

                    var combinedDropChance = box.pools.Sum(x => x.dropChance);

                    Utility.ArrayDisplay(box.pools,
                        (boxPool, boxPoolRemoveButton) =>
                        {
                            var referencedPool =
                                configService.ConfigFile.pools.FirstOrDefault(x => x.Guid == boxPool.poolId);

                            if (referencedPool == null)
                            {
                                boxPoolRemoveButton($"##{boxPool.Guid}");
                                ImGui.TextWrapped(
                                    $"{Codicons.Error} the pool {boxPool.poolId} referenced in this box doesn't exist (or the guid failed to deserialize). " +
                                    "we're gonna skip that pool for now but please fix that (or ping kiev), things will probably go very wrong otherwise.");
                                return;
                            }

                            boxPoolRemoveButton($"##{boxPool.Guid}");
                            ImGui.SameLine();
                            if (!ImGui.TreeNode($"{boxPool.Guid}", $"{referencedPool.name} ({referencedPool.Guid})"))
                                return;

                            ImGui.InputInt("Drop Chance", ref boxPool.dropChance);
                            ImGui.TextWrapped(
                                $"Drop percentage (compared to other pools in this box): {(float)boxPool.dropChance / combinedDropChance:P3}%");

                            // Unfinished?
                            // Utility.ArrayDisplay(boxPool.conditions,
                            //     (conditionContainer, conditionContainerRemoveButton) =>
                            //     {
                            //         ImGui.PushID($"{((UniqueObject)conditionContainer.data).Guid}");
                            //         ImGui.SeparatorText($"{conditionContainer.conditionType} ({((UniqueObject)conditionContainer.data).Guid})");
                            //         switch (conditionContainer.conditionType)
                            //         {
                            //             //case PoolConditionContainer.ConditionType.DropChance:
                            //             //    {
                            //             //        var condition = (DropChanceConditionData)conditionContainer.data;
                            //
                            //             //        ImGui.InputInt("Drop Chance", ref condition.dropChance);
                            //             //        ImGui.TextWrapped($"Drop percentage (compared to other pools in this box): {(float)condition.dropChance / combinedDropChance:P3}%");
                            //             //        break;
                            //             //    }
                            //             default:
                            //                 {
                            //                     ImGui.TextWrapped(
                            //                         $"{Codicons.Error} The condition type {conditionContainer.conditionType} isn't implemented here for some reason.");
                            //                     break;
                            //                 }
                            //         }
                            //
                            //         conditionContainerRemoveButton("Delete Condition");
                            //         ImGui.PopID();
                            //     });


                            if (boxPool.conditions.Count > 0) ImGui.Separator();

                            ConditionUi(boxPool.conditions);

                            ImGui.Separator();

                            ImGui.TreePop();
                            ImGui.Separator();
                        });

                    {
                        var pools = configService.ConfigFile.pools;
                        // .Where(x => box.pools.All(y => y.poolId != x.Guid)).ToArray()
                        var index = 0;
                        var comboBoxItems = pools.Select(x => $"{x.name} ({x.Guid})").Prepend("").ToArray();

                        ImGui.Combo("Add pool to box", ref index, comboBoxItems, comboBoxItems.Length);

                        if (index != 0)
                        {
                            box.pools.Add(new BoxPool { poolId = pools[index - 1].Guid });
                        }
                    }

                    ImGui.TreePop();
                    ImGui.Separator();
                });

            if (ImGui.Button($"{Codicons.Plus} Add Box"))
            {
                configService.ConfigFile.boxes.Add(new RewardBox());
            }
        }

        if (ImGui.CollapsingHeader("Pools"))
        {
            Utility.ArrayDisplay(configService.ConfigFile.pools,
                (pool, poolRemoveButton) =>
                {
                    poolRemoveButton($"##{pool.Guid}");
                    ImGui.SameLine();
                    if (!ImGui.TreeNode($"{pool.Guid}", $"{pool.name} ({pool.Guid})"))
                        return;

                    ImGui.InputText("Name", ref pool.name, 4096);

                    Utility.ArrayDisplay(pool.rewards,
                        (poolReward, poolRewardRemoveButton) =>
                        {
                            if (!ImGui.TreeNode($"{poolReward.Guid}", $"{poolReward.name} ({poolReward.Guid})")) return;

                            ImGui.InputText("Name", ref poolReward.name, Utility.IMGUI_DEFAULT_MAX_LENGTH);
                            ImGui.Checkbox("Reward is unique", ref poolReward.isUnique);
                            ImGui.SetItemTooltip("If this reward is rolled, no one else will be able to roll it.");

                            //if (ImGui.TreeNode("Response"))
                            //{
                            //    GachaMessageEditor.MessageEditor(poolReward.Guid, ref poolReward.response);

                            //    ImGui.TreePop();
                            //}

                            RewardActionUi(poolReward.actions);

                            ImGui.Separator();

                            poolRewardRemoveButton("Remove Reward");

                            ImGui.TreePop();
                            ImGui.Separator();
                        });

                    if (ImGui.Button($"{Codicons.Plus} Add New Reward"))
                    {
                        var reward = new Reward();
                        reward.actions.Add(new RewardActionContainer(RewardActionContainer.ActionType.SetResponse));
                        pool.rewards.Add(reward);
                    }

                    ImGui.SameLine();

                    {
                        var existingRewards = configService.ConfigFile.pools
                            .SelectMany(x => x.rewards.Select(reward => new { PoolName = x.name, Reward = reward }))
                            .ToArray();
                        var index = 0;
                        var comboBoxItems = existingRewards
                            .Select(x => $"{x.Reward.name} ({x.Reward.Guid}) - Pool: {x.PoolName}").Prepend("")
                            .ToArray();

                        ImGui.Combo("Copy reward", ref index, comboBoxItems, comboBoxItems.Length);

                        if (index != 0)
                        {
                            var reward = existingRewards[index - 1].Reward;

                            HackGuidResetConverter.shouldResetGuid = true;

                            // shouldn't ever be null I hope
                            var copiedReward =
                                JsonConvert.DeserializeObject<Reward>(JsonConvert.SerializeObject(reward))!;

                            HackGuidResetConverter.shouldResetGuid = false;

                            pool.rewards.Add(copiedReward);
                        }
                    }

                    // poolRemoveButton("Remove pool");

                    ImGui.TreePop();
                }, poolToRemove =>
                {
                    foreach (var box in configService.ConfigFile.boxes)
                    {
                        box.pools.RemoveAll(x => x.poolId == poolToRemove.Guid);
                    }
                });

            if (ImGui.Button($"{Codicons.Plus} Add pool"))
            {
                configService.ConfigFile.pools.Add(new RewardPool());
            }
        }

        if (ImGui.CollapsingHeader("Items and Categories"))
        {
            if (ImGui.TreeNode("Categories"))
            {
                Utility.ArrayDisplay(configService.ConfigFile.categories,
                    (category, removeCategoryButton) =>
                    {
                        removeCategoryButton($"##{category.Guid}");
                        ImGui.SameLine();
                        if (!ImGui.TreeNode($"{category.Guid}", $"{category.name} ({category.Guid})"))
                            return;

                        ImGui.InputText("Name", ref category.name, Utility.IMGUI_DEFAULT_MAX_LENGTH);
                        ImageUrlInput("Image URL (optional)", ref category.imageUrl,
                            "If set, the image will be used as the category header instead of a plain text header.");

                        ImGui.TreePop();
                    });

                if (ImGui.Button("Add category"))
                {
                    configService.ConfigFile.categories.Add(new ItemCategory());
                }

                ImGui.TreePop();
            }

            if (ImGui.TreeNode("Items"))
            {
                Utility.ArrayDisplay(configService.ConfigFile.items,
                    (item, removeItemButton) =>
                    {
                        removeItemButton($"##{item.Guid}");
                        ImGui.SameLine();
                        if (!ImGui.TreeNode($"{item.Guid}", $"{item.name} ({item.Guid})"))
                            return;

                        ImGui.InputText("Name", ref item.name, Utility.IMGUI_DEFAULT_MAX_LENGTH);

                        ImGui.InputTextMultiline("Description", ref item.description, Utility.IMGUI_DEFAULT_MAX_LENGTH,
                            new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 4));
                        {
                            var categories = configService.ConfigFile.categories;
                            var selectedCategory = categories.FirstOrDefault(x => x.Guid == item.categoryGuid);
                            var index = selectedCategory != null ? categories.IndexOf(selectedCategory) + 1 : 0;
                            var comboBoxItems = categories.Select(x => $"{x.name} ({x.Guid})").Prepend("").ToArray();

                            ImGui.Combo("Category", ref index, comboBoxItems, comboBoxItems.Length);

                            item.categoryGuid = index == 0 ? null : categories[index - 1].Guid;
                        }

                        ImGui.Spacing();

                        ImGui.Checkbox("Should always show in inventory", ref item.shouldAlwaysShowInInventory);
                        ImGui.SetItemTooltip(
                            "Think how Guya works, where you can always see the cards in the inventory.");

                        ImageUrlInput("Image URL", ref item.imageUrl);

                        ImGui.BeginDisabled(!item.shouldAlwaysShowInInventory);

                        ImageUrlInput("Image URL (Silhouette)", ref item.imageSilhouetteUrl,
                            "Shown as the item preview when the item is not in the inventory, " +
                            "but is marked as \"Should always show in inventory\".");

                        ImGui.EndDisabled();

                        ImGui.Spacing();

                        ImGui.InputInt("Sell Price", ref item.sellPrice);
                        item.sellPrice = Math.Max(0, item.sellPrice);

                        ImGui.Checkbox("Can be used", ref item.hasUseActions);
                        if (item.hasUseActions && ImGui.TreeNode("Use Actions"))
                        {
                            RewardActionUi(item.useActions);
                            ImGui.TreePop();
                        }

                        ImGui.Checkbox("Can be equipped", ref item.hasEquipActions);
                        if (item.hasEquipActions && ImGui.TreeNode("Equip Actions"))
                        {
                            RewardActionUi(item.equipActions);
                            ImGui.TreePop();
                        }

                        if (item.hasEquipActions && ImGui.TreeNode("De-Equip Actions"))
                        {
                            RewardActionUi(item.deEquipActions);
                            ImGui.TreePop();
                        }

                        ImGui.TreePop();
                    });

                if (ImGui.Button("Add item"))
                {
                    configService.ConfigFile.items.Add(new RewardItem()
                    {
                        useActions = [new RewardActionContainer(RewardActionContainer.ActionType.SetResponse)],
                        equipActions = [new RewardActionContainer(RewardActionContainer.ActionType.SetResponse)],
                        deEquipActions = [new RewardActionContainer(RewardActionContainer.ActionType.SetResponse)],
                    });
                }

                ImGui.TreePop();
            }
        }

        if (ImGui.CollapsingHeader("Shop Wares"))
        {
            Utility.ArrayDisplay(configService.ConfigFile.shopWares, (ware, removeButton) =>
            {
                removeButton($"##{ware.Guid}");
                ImGui.SameLine();
                if (!ImGui.TreeNode($"{ware.Guid}", $"{ware.name} ({ware.Guid})"))
                    return;

                ImGui.InputText("Name", ref ware.name, 256);
                ImGui.InputTextMultiline("Description", ref ware.description, 512,
                    new Vector2(0, ImGui.GetTextLineHeightWithSpacing() * 4));

                ImGui.Spacing();

                ImGui.InputInt("Cost", ref ware.cost);

                ImGui.Spacing();

                if (ImGui.TreeNode("Actions"))
                {
                    RewardActionUi(ware.actionsUponRedeem);
                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Conditions"))
                {
                    ConditionUi(ware.conditions);
                }
            });

            if (ImGui.Button("Add ware"))
            {
                var ware = new ShopWare();
                ware.actionsUponRedeem.Add(new RewardActionContainer(RewardActionContainer.ActionType.SetResponse));

                configService.ConfigFile.shopWares.Add(ware);
            }
        }

        ImGui.End();
    }

    private void ImageUrlInput(string label, ref string url, string? inputTooltip = null)
    {
        ImGui.InputText(label, ref url, Utility.IMGUI_DEFAULT_MAX_LENGTH);
        if (inputTooltip != null)
            ImGui.SetItemTooltip(inputTooltip);
        if (Utility.DiscordRegex().IsMatch(url))
            ImGui.TextColored(new Vector4(1, 0, 0, 1),
                "Don't use Discord links, they don't like it and it'll break in like, " +
                "a day cuz of the new auth stuff.");

        ImageFromUrl(url, 200);
    }

    private void ImageFromUrl(string url, int maxHeight)
    {
        var texture = imageLoader.GetOrDownloadImage(url);
        texture ??= imageLoader.IsLoadingImage(url)
            ? imageLoader.GetOrDownloadImage(ImageLoader.loadingUrl)
            : imageLoader.GetOrDownloadImage(ImageLoader.notFoundUrl);
        if (texture != null)
        {
            var height = maxHeight;
            var width = texture.texture.Width * ((float)height / texture.texture.Height);

            Vector2 pos = ImGui.GetCursorScreenPos();

            ImGui.Image(texture.imguiTexturePtr, new Vector2(width, height));

            if (ImGui.BeginItemTooltip())
            {
                var io = ImGui.GetIO();

                float regionSz = Math.Min(maxHeight, 64.0f);
                float regionX = io.MousePos.X - pos.X - regionSz * 0.5f;
                float regionY = io.MousePos.Y - pos.Y - regionSz * 0.5f;
                float zoom = 4.0f;

                if (regionX < 0.0f) regionX = 0.0f;
                else if (regionX > width - regionSz) regionX = width - regionSz;

                if (regionY < 0.0f) regionY = 0.0f;
                else if (regionY > height - regionSz) regionY = height - regionSz;

#if DEBUG
                ImGui.Text($"Min: ({regionX:F2}, {regionY:F2})");
                ImGui.Text($"Max: ({regionX + regionSz:F2}, {regionY + regionSz:F2})");
#endif

                Vector2 uv0 = new(regionX / width, regionY / height);
                Vector2 uv1 = new((regionX + regionSz) / width, (regionY + regionSz) / height);
                ImGui.Image(texture.imguiTexturePtr, new Vector2(regionSz * zoom, regionSz * zoom), uv0, uv1);

                ImGui.EndTooltip();
            }
        }
    }

    private void ConditionUi(List<PoolConditionContainer> conditions)
    {
        Utility.ArrayDisplay(conditions, PoolConditionContainerUi);

        var types = Enum.GetValues<PoolConditionContainer.ConditionType>();
        var index = 0;
        var comboBoxItems = types.Select(x => $"{x}").Prepend("").ToArray();

        ImGui.Combo("Add condition", ref index, comboBoxItems, comboBoxItems.Length);

        if (index != 0)
        {
            var conditionType = types[index - 1];

            conditions.Add(new PoolConditionContainer(conditionType));
        }
    }

    private void PoolConditionContainerUi(PoolConditionContainer poolConditionContainer, Action<string> removeButton)
    {
        var guid = ((UniqueObject)poolConditionContainer.data).Guid;

        removeButton($"##{guid}");
        ImGui.SameLine();
        if (!ImGui.TreeNode($"{guid}",
                $"{poolConditionContainer.conditionType} ({guid})"))
            return;

        switch (poolConditionContainer.conditionType)
        {
            case PoolConditionContainer.ConditionType.HasItemCondition:
            {
                var condition = (HasItemCondition)poolConditionContainer.data;

                condition.itemGuid = ItemSelector(condition.itemGuid);
                break;
            }

            case PoolConditionContainer.ConditionType.NotCondition:
            {
                var condition = (NotCondition)poolConditionContainer.data;

                ConditionUi(condition.conditions);
                break;
            }
            case PoolConditionContainer.ConditionType.OrCondition:
            {
                var condition = (OrCondition)poolConditionContainer.data;

                ConditionUi(condition.conditions);
                break;
            }
            case PoolConditionContainer.ConditionType.HasRoleCondition:
            {
                var condition = (HasRoleCondition)poolConditionContainer.data;

                condition.roleId = RoleIdEditor(condition.roleId);
                break;
            }
            default:
                ImGui.TextWrapped(
                    $"{Codicons.Error} The condition type {poolConditionContainer.conditionType} isn't implemented here for some reason.");
                break;
        }

        ImGui.Separator();

        ImGui.TreePop();
    }

    private void RewardActionUi(List<RewardActionContainer> actions)
    {
        Utility.ArrayDisplay(actions, RewardActionContainerUi);

        var conditions = Enum.GetValues<RewardActionContainer.ActionType>();
        var index = 0;
        var comboBoxItems = conditions.Select(x => $"{x}").Prepend("").ToArray();

        ImGui.Combo("Add action", ref index, comboBoxItems, comboBoxItems.Length);

        if (index != 0)
        {
            var conditionType = conditions[index - 1];

            actions.Add(new RewardActionContainer(conditionType));
        }
    }

    private void RewardActionContainerUi(RewardActionContainer rewardActionContainer,
        Action<string> rewardActionContainerRemoveButton)
    {
        var guid = ((UniqueObject)rewardActionContainer.data).Guid;

        rewardActionContainerRemoveButton($"##{guid}");
        ImGui.SameLine();
        if (!ImGui.TreeNode($"{guid}",
                $"{rewardActionContainer.actionType} ({guid})"))
            return;

        switch (rewardActionContainer.actionType)
        {
            case RewardActionContainer.ActionType.SetResponse:
            {
                var rewardAction = (SetResponseData)rewardActionContainer.data;

                GachaMessageEditor.MessageEditor(guid, ref rewardAction.response);
                break;
            }

            case RewardActionContainer.ActionType.SendMessage:
            {
                var rewardAction = (SendMessageActionData)rewardActionContainer.data;

                var id = rewardAction.channelId.ToString();
                ImGui.InputText("Channel ID", ref id, Utility.IMGUI_DEFAULT_MAX_LENGTH);
                rewardAction.channelId = ValidationUtilities.StripAndConvertToULong(id);
                GachaMessageEditor.MessageEditor(guid, ref rewardAction.message);

                break;
            }
            case RewardActionContainer.ActionType.GrantRole:
            {
                var rewardAction = (RoleActionData)rewardActionContainer.data;

                rewardAction.roleId = RoleIdEditor(rewardAction.roleId);

                break;
            }
            case RewardActionContainer.ActionType.RemoveRole:
            {
                var rewardAction = (RoleActionData)rewardActionContainer.data;

                rewardAction.roleId = RoleIdEditor(rewardAction.roleId);

                break;
            }
            case RewardActionContainer.ActionType.AddItem:
            {
                var rewardAction = (ItemActionData)rewardActionContainer.data;

                rewardAction.itemGuid = ItemSelector(rewardAction.itemGuid);
                break;
            }

            case RewardActionContainer.ActionType.RemoveItem:
            {
                var rewardAction = (ItemActionData)rewardActionContainer.data;

                rewardAction.itemGuid = ItemSelector(rewardAction.itemGuid);
                break;
            }

            case RewardActionContainer.ActionType.ChangeNickname:
            {
                var rewardAction = (ChangeNicknameActionData)rewardActionContainer.data;

                ImGui.InputText("Nickname", ref rewardAction.newNickname, 32);
                break;
            }

            case RewardActionContainer.ActionType.ExecuteAfterDelay:
            {
                var rewardAction = (ExecuteAfterDelayActionData)rewardActionContainer.data;

                ImGui.InputInt("Minimum delay (seconds)", ref rewardAction.delaySecondsMin);
                rewardAction.delaySecondsMin = Math.Max(rewardAction.delaySecondsMin, 0);

                ImGui.InputInt("Maximum delay (seconds)", ref rewardAction.delaySecondsMax);
                rewardAction.delaySecondsMax = Math.Max(rewardAction.delaySecondsMin, rewardAction.delaySecondsMax);

                if (ImGui.TreeNode("Actions to execute after delay"))
                {
                    RewardActionUi(rewardAction.actions);

                    ImGui.TreePop();
                }

                break;
            }

            default:
                ImGui.TextWrapped(
                    $"{Codicons.Error} The action type {rewardActionContainer.actionType} isn't implemented here for some reason.");
                break;
        }

        ImGui.Separator();

        ImGui.TreePop();
    }

    [Pure]
    private static ulong RoleIdEditor(ulong roleId)
    {
        var id = roleId.ToString();
        ImGui.InputText("Role ID", ref id, Utility.IMGUI_DEFAULT_MAX_LENGTH);
        return ValidationUtilities.StripAndConvertToULong(id);
    }

    [Pure]
    private Guid ItemSelector(Guid currentGuid)
    {
        var items = configService.ConfigFile!.items;
        var selectedItem = items.FirstOrDefault(x => x.Guid == currentGuid);
        var index = selectedItem != null ? items.IndexOf(selectedItem) + 1 : 0;
        var comboBoxItems = items.Select(x => $"{x.name} ({x.Guid})").Prepend("").ToArray();

        ImGui.Combo("Item", ref index, comboBoxItems, comboBoxItems.Length);

        if (index != 0)
        {
            return items[index - 1].Guid;
        }

        return currentGuid;
    }
}