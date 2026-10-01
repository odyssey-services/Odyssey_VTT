using NUnit.Framework;
using Odyssey.Unity.Client;
using UnityEngine.UIElements;

namespace Odyssey.Tests.Unity.EditMode
{
    /// <summary>ODY-S11-220 (polish P1 item 10): skeleton placeholder structure and shimmer stepping.</summary>
    public sealed class OdySkeletonTests
    {
        [Test]
        public void Skeleton_HasGreyBars_TheLastOneShorter_AndIgnoresThePointer()
        {
            var skeleton = new OdySkeleton("sheet-loading", lines: 4);
            Assert.That(skeleton.LineCount, Is.EqualTo(4));
            Assert.That(skeleton.Element.ClassListContains(OdyClasses.Skeleton), Is.True);
            Assert.That(skeleton.Element.Q<VisualElement>("sheet-loading-bar-3").ClassListContains(OdyClasses.SkeletonBarShort), Is.True);
            Assert.That(skeleton.Element.Q<VisualElement>("sheet-loading-bar-0").ClassListContains(OdyClasses.SkeletonBarShort), Is.False);
            Assert.That(skeleton.Element.pickingMode, Is.EqualTo(PickingMode.Ignore));
        }

        [Test]
        public void Shimmer_TogglesEveryHalfPeriod_StopsUnderReducedMotion_AndOnStop()
        {
            var host = new VisualElement();
            var skeleton = new OdySkeleton("loading");
            host.Add(skeleton.Element);
            skeleton.StartShimmer();

            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs - 1);
            Assert.That(skeleton.IsBright, Is.False);
            skeleton.Advance(1);
            Assert.That(skeleton.IsBright, Is.True, "bright half");
            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs);
            Assert.That(skeleton.IsBright, Is.False, "and back: one 1.3 s cycle");

            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs);
            host.AddToClassList(OdyClasses.ReducedMotion);
            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs * 3);
            Assert.That(skeleton.IsBright, Is.False, "reduced motion: still grey bars");

            host.RemoveFromClassList(OdyClasses.ReducedMotion);
            skeleton.Advance(OdySkeleton.ShimmerHalfPeriodMs);
            skeleton.StopShimmer();
            Assert.That(skeleton.IsBright, Is.False, "stopping resets the look");
        }
    }
}
