using System;
using Esnaf.Core;

namespace Esnaf.Domain.Economy
{
    /// <summary>Nakit: defter bakiyesi.</summary>
    public sealed class CashWealthContributor : IWealthContributor
    {
        private readonly EconomyState _state;

        public CashWealthContributor(EconomyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _state = state;
        }

        public string Key
        {
            get { return WealthKeys.Cash; }
        }

        public Money GetValue()
        {
            return _state.Cash;
        }
    }

    /// <summary>İşletme varlıkları: yatırımların toplamı (raf yükseltme, ekipman).</summary>
    public sealed class BusinessAssetsWealthContributor : IWealthContributor
    {
        private readonly EconomyState _state;

        public BusinessAssetsWealthContributor(EconomyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _state = state;
        }

        public string Key
        {
            get { return WealthKeys.BusinessAssets; }
        }

        public Money GetValue()
        {
            return _state.BusinessAssets;
        }
    }

    /// <summary>Bekleyen ekspertiz ücretleri: ürüne eklenene veya gider yazılana kadar servet kaybı sayılmaz.</summary>
    public sealed class PendingAppraisalWealthContributor : IWealthContributor
    {
        private readonly EconomyState _state;

        public PendingAppraisalWealthContributor(EconomyState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            _state = state;
        }

        public string Key
        {
            get { return WealthKeys.PendingAppraisals; }
        }

        public Money GetValue()
        {
            return _state.PendingAppraisalTotal();
        }
    }
}
