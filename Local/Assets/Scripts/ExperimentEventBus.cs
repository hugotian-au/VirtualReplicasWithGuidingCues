using System;

public static class ExperimentEventBus
{
    // 定义全局事件：当条件改变时触发
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
                // 触发事件，通知所有订阅者
                OnConditionChanged?.Invoke(_currentCondition);
            }
        }
    }
}