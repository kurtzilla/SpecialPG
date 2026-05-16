#nullable enable
using System.Collections.Generic;

namespace SpecialPG;

public sealed class DecorChunkPool
{
    private readonly Stack<DecorChunkView> _available = new();

    public DecorChunkView Acquire()
    {
        if (_available.Count > 0)
        {
            var view = _available.Pop();
            view.Visible = true;
            return view;
        }

        return new DecorChunkView();
    }

    public void Release(DecorChunkView view)
    {
        view.ReleaseToPool();
        _available.Push(view);
    }
}
