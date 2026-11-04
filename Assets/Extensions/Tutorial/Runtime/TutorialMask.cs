using Starter.Extensions.UI;
using UnityEngine;

namespace Starter.Extensions.Tutorial
{
    [AddComponentMenu("Starter/Tutorial/Mask")]
    public sealed class TutorialMask : HollowOutMask
    {
        public void SetTwoTargets(RectTransform first, RectTransform second) { SetTargets(first, second); }
    }
}
