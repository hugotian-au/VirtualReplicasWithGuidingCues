using System;

public static class ExperimentEventBus
{
    // 全局实验条件变更事件
    public static event Action<string> OnConditionChanged;

    // 存储当前状态，解决中途加入或先后顺序问题
    private static string _currentCondition = "";

    public static string CurrentCondition
    {
        get => _currentCondition;
        set
        {
            if (_currentCondition != value)
            {
                _currentCondition = value;
                OnConditionChanged?.Invoke(_currentCondition);
            }
        }
    }
}
