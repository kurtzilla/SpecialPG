#nullable enable
using System.Collections.Generic;

namespace SpecialPG;

public sealed class EntityViewPool
{
    private readonly Stack<EntityView> _available = new();

    public EntityView Acquire()
    {
        if (_available.Count > 0)
        {
            var view = _available.Pop();
            view.Visible = true;
            return view;
        }

        return new EntityView();
    }

    public void Release(EntityView view)
    {
        view.ReleaseToPool();
        _available.Push(view);
    }
}
