using System.Collections.Generic;

public sealed class Account
{
    // Readonly 필수 : 재화는 패치 이후에 생성이 되어야 하기 때문.
    private readonly Dictionary<CurrencyType, long> _balances = new();

    public Account(IEnumerable<CurrencyType> supportedCurrencies)
    {
        foreach (var c in supportedCurrencies)
            _balances[c] = 0;
    }

    private bool IsSupported(CurrencyType currency)
    {
        return _balances.ContainsKey(currency);
    }

    // 잔액 확인 용도
    public long GetBalance(CurrencyType currency) => _balances.TryGetValue(currency, out var v) ? v : 0;

    // 돈을 보낼 수 있는지 확인한다. 안정성 메서드 필요.
    public bool CanSpend(CurrencyType currency, long amount)
    {
        if (amount <= 0) return false;
        if (!IsSupported(currency)) return false;
        
        return GetBalance(currency) >= amount;
    }

    private long Add(CurrencyType currency, long amount)
    {
        if (!IsSupported(currency))
            throw new System.ArgumentException($"Unsupported currency: {currency}", nameof(currency));

        var curMoney = GetBalance(currency);
        var afterMoney = checked(curMoney + amount);

        _balances[currency] = afterMoney;
        return amount;
    }

    public bool TrySpend(CurrencyType currency, long amount)
    {
        if (!CanSpend(currency, amount)) return false;

        var beforeMoney = GetBalance(currency);
        _balances[currency] = beforeMoney - amount;
        return true;
    }

    public void Reset()
    {
        var keys = new List<CurrencyType>(_balances.Keys);
        foreach (var key in keys)
            _balances[key] = 0;
    }

    public bool TryAdd(CurrencyType currency, long amount)
    {
        if (amount <= 0) return false;
        if (!IsSupported(currency)) return false;

        try
        {
            Add(currency, amount);
            return true;
        }
        catch (System.OverflowException)
        {
            return false;
        }
    }
}