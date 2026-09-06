using System;
using System.Collections.Generic;
using SeaVillage.Utilities;

namespace SeaVillage.Core
{
    /// <summary>
    /// 화폐 통합 관리 매니저 
    /// </summary>
    public class CurrencyManager : Singleton<CurrencyManager>
    {
        private static readonly CurrencyType[] SupportedCurrencies = { CurrencyType.Gold };

        private Account _playerAccount;
        private readonly Dictionary<int, Account> _customerAccounts = new();

        public event Action<long> OnPlayerGoldChanged;
        public event Action<int, long> OnCustomerGoldChanged;

        protected override void Awake()
        {
            base.Awake();

            if (_playerAccount == null)
                _playerAccount = new Account(SupportedCurrencies);
        }

        #region Player
        public long GetPlayerBalance(CurrencyType currency)
        {
            if (currency != CurrencyType.Gold) return 0L;
            return _playerAccount.GetBalance(currency);
        }

        public bool CanPlayerSpend(CurrencyType currency, long amount)
        {
            if (currency != CurrencyType.Gold) return false;
            return _playerAccount.CanSpend(currency, amount);
        }

        public bool TrySpendPlayer(CurrencyType currency, long amount)
        {
            if (currency != CurrencyType.Gold) return false;
            if (!_playerAccount.TrySpend(currency, amount)) return false;

            InvokePlayerBalanceChanged();
            return true;
        }

        public bool TryAddPlayer(CurrencyType currency, long amount)
        {
            if (currency != CurrencyType.Gold) return false;
            if (!_playerAccount.TryAdd(currency, amount)) return false;

            InvokePlayerBalanceChanged();
            return true;
        }

        public bool SetPlayerBalance(CurrencyType currency, long targetAmount)
        {
            if (currency != CurrencyType.Gold) return false;

            long safeTarget = Math.Max(0L, targetAmount);
            long current = _playerAccount.GetBalance(currency);

            if (current == safeTarget)
            {
                InvokePlayerBalanceChanged();
                return true;
            }

            long delta = safeTarget - current;
            bool result = delta > 0
                ? _playerAccount.TryAdd(currency, delta)
                : _playerAccount.TrySpend(currency, -delta);

            if (!result)
                return false;

            InvokePlayerBalanceChanged();
            return true;
        }

        public bool TryGrantPlayerReward(long amount)
        {
            return TryAddPlayer(CurrencyType.Gold, amount);
        }
        #endregion

        #region Customer
        public bool RegisterCustomerAccount(int customerId, long initialGold)
        {
            if (customerId <= 0 || _customerAccounts.ContainsKey(customerId))
                return false;

            var account = new Account(SupportedCurrencies);
            long safeGold = Math.Max(0L, initialGold);

            if (safeGold > 0 && !account.TryAdd(CurrencyType.Gold, safeGold))
                return false;

            _customerAccounts[customerId] = account;
            InvokeCustomerBalanceChanged(customerId);
            return true;
        }

        public bool UnregisterCustomerAccount(int customerId)
        {
            return _customerAccounts.Remove(customerId);
        }

        public bool HasCustomerAccount(int customerId)
        {
            return _customerAccounts.ContainsKey(customerId);
        }

        public long GetCustomerBalance(int customerId, CurrencyType currency)
        {
            if (currency != CurrencyType.Gold) return 0L;
            return _customerAccounts.TryGetValue(customerId, out var account)
                ? account.GetBalance(currency)
                : 0L;
        }

        public bool TryGrantCustomerReward(int customerId, long amount)
        {
            if (!_customerAccounts.TryGetValue(customerId, out var account))
                return false;

            if (!account.TryAdd(CurrencyType.Gold, amount))
                return false;

            InvokeCustomerBalanceChanged(customerId);
            return true;
        }

        public bool TrySetCustomerBalance(int customerId, long targetAmount)
        {
            if (!_customerAccounts.TryGetValue(customerId, out var account))
                return false;

            long safeTarget = Math.Max(0L, targetAmount);
            long current = account.GetBalance(CurrencyType.Gold);

            if (current == safeTarget)
            {
                InvokeCustomerBalanceChanged(customerId);
                return true;
            }

            long delta = safeTarget - current;
            bool result = delta > 0
                ? account.TryAdd(CurrencyType.Gold, delta)
                : account.TrySpend(CurrencyType.Gold, -delta);

            if (!result)
                return false;

            InvokeCustomerBalanceChanged(customerId);
            return true;
        }

        public IReadOnlyList<CustomerWallet> GetCustomerWalletsSnapshot()
        {
            var snapshot = new List<CustomerWallet>(_customerAccounts.Count);

            foreach (var entry in _customerAccounts)
            {
                snapshot.Add(new CustomerWallet
                {
                    CustomerId = entry.Key,
                    Gold = entry.Value.GetBalance(CurrencyType.Gold)
                });
            }

            return snapshot;
        }
        #endregion

        #region Transfer
        public bool TryTransferPlayerToCustomer(int customerId, long amount)
        {
            if (!_customerAccounts.TryGetValue(customerId, out var customerAccount))
                return false;

            bool transferred = AccountTransfer.TryTransfer(_playerAccount, customerAccount, CurrencyType.Gold, amount);
            if (!transferred)
                return false;

            InvokePlayerBalanceChanged();
            InvokeCustomerBalanceChanged(customerId);
            return true;
        }

        public bool TryTransferCustomerToPlayer(int customerId, long amount)
        {
            if (!_customerAccounts.TryGetValue(customerId, out var customerAccount))
                return false;

            bool transferred = AccountTransfer.TryTransfer(customerAccount, _playerAccount, CurrencyType.Gold, amount);
            if (!transferred)
                return false;

            InvokeCustomerBalanceChanged(customerId);
            InvokePlayerBalanceChanged();
            return true;
        }
        #endregion

        private void InvokePlayerBalanceChanged()
        {
            OnPlayerGoldChanged?.Invoke(_playerAccount.GetBalance(CurrencyType.Gold));
        }

        private void InvokeCustomerBalanceChanged(int customerId)
        {
            if (!_customerAccounts.TryGetValue(customerId, out var account))
                return;

            OnCustomerGoldChanged?.Invoke(customerId, account.GetBalance(CurrencyType.Gold));
        }
    }
}