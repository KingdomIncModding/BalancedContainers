using System;
using KingdomFoundation.Api;
using UnityEngine;

namespace BalancedContainers
{
    public sealed class ContainerTarget : IEquatable<ContainerTarget>
    {
        public static readonly ContainerTarget None = new ContainerTarget(ContainerTargetKind.None, string.Empty);

        public ContainerTargetKind Kind { get; }
        public string Id { get; }
        public bool IsAssigned => Kind != ContainerTargetKind.None && !string.IsNullOrEmpty(Id);

        public ContainerTarget(ContainerTargetKind kind, string id)
        {
            Kind = kind;
            Id = id ?? string.Empty;
        }

        public static ContainerTarget ForItem(string itemId)
        {
            return string.IsNullOrEmpty(itemId)
                ? None
                : new ContainerTarget(ContainerTargetKind.Item, itemId);
        }

        public static ContainerTarget ForCurrency(GameCurrency currency)
        {
            return new ContainerTarget(ContainerTargetKind.Currency, currency.ToString());
        }

        public bool TryGetCurrency(out GameCurrency currency)
        {
            if (Kind != ContainerTargetKind.Currency || string.IsNullOrEmpty(Id))
            {
                currency = default;
                return false;
            }

            return Enum.TryParse(Id, ignoreCase: true, out currency);
        }

        public GameItemType ResolveItem()
        {
            if (Kind != ContainerTargetKind.Item || string.IsNullOrEmpty(Id))
            {
                return null;
            }

            return FoundationApi.ItemAccess?.Find(Id);
        }

        public string GetDisplayName()
        {
            if (!IsAssigned)
            {
                return "Regular Container";
            }

            if (TryGetCurrency(out var currency))
            {
                return FormatCurrencyName(currency);
            }

            var item = ResolveItem();
            if (item == null)
            {
                return ContainerUiTheme.TranslateName(null, Id);
            }

            string name = item.GetDisplayName();
            if (string.IsNullOrWhiteSpace(name))
            {
                name = item.PreviewDisplayName;
            }

            return ContainerUiTheme.TranslateName(name, item.Identifier ?? Id);
        }

        public Sprite GetIcon()
        {
            if (!IsAssigned)
            {
                return FoundationApi.Decorations?.ContainerIcon;
            }

            if (TryGetCurrency(out var currency))
            {
                if (currency == GameCurrency.Gold)
                {
                    return FoundationApi.UiAccess?.CurrencyIcon("Gold", 0) ?? currency.GetIcon();
                }

                return currency.GetIcon();
            }

            var item = ResolveItem();
            return item?.GetIcon();
        }

        public bool Matches(ContainerTargetKind kind, string id)
        {
            return Kind == kind && string.Equals(Id, id ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        public bool Equals(ContainerTarget other)
        {
            if (ReferenceEquals(other, null))
            {
                return false;
            }

            return Kind == other.Kind && string.Equals(Id, other.Id, StringComparison.OrdinalIgnoreCase);
        }

        public override bool Equals(object obj) => Equals(obj as ContainerTarget);

        public override int GetHashCode()
        {
            return ((int)Kind * 397) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(Id ?? string.Empty);
        }

        internal static string FormatCurrencyName(GameCurrency currency)
        {
            switch (currency)
            {
                case GameCurrency.Gold: return "Gold";
                case GameCurrency.Requisition: return "Requisition";
                case GameCurrency.Medal: return "Medals";
                case GameCurrency.Ring: return "Rings";
                case GameCurrency.Mana: return "Mana";
                case GameCurrency.Crown: return "Crowns";
                case GameCurrency.Ducat: return "Ducats";
                case GameCurrency.Ingenuity: return "Ingenuity";
                case GameCurrency.Faith: return "Faith";
                default:
                    string fallback = currency.GetDisplayName();
                    return string.IsNullOrWhiteSpace(fallback) ? currency.ToString() : fallback;
            }
        }
    }
}

