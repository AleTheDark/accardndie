using System.Reflection;
using AccardND.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace AccardND.GameCore.Tests
{
    public sealed class TurnActionLayoutTests
    {
        private static readonly MethodInfo CalculateVerticalLift = typeof(PrototypeCardView).GetMethod(
            "CalculateTurnActionVerticalLift",
            BindingFlags.NonPublic | BindingFlags.Static);
        private static readonly MethodInfo SetTurnActionSlot = typeof(PrototypeCardView).GetMethod(
            "SetTurnActionSlot",
            BindingFlags.NonPublic | BindingFlags.Static);

        [Test]
        public void ThreeActions_PlacesCenterAboveMatchingExtremes()
        {
            float[] lifts = LiftsFor(3);

            Assert.That(lifts[0], Is.EqualTo(lifts[2]).Within(0.0001f));
            Assert.That(lifts[1], Is.GreaterThan(lifts[0]));
        }

        [Test]
        public void ThreeActions_IgnoresEdgeBiasAndKeepsOriginalArc()
        {
            float[] centered = LiftsFor(3);
            float[] rightEdge = LiftsFor(3, 1f);
            float[] leftEdge = LiftsFor(3, -1f);

            for (int index = 0; index < centered.Length; index++)
            {
                Assert.That(rightEdge[index], Is.EqualTo(centered[index]).Within(0.0001f));
                Assert.That(leftEdge[index], Is.EqualTo(centered[index]).Within(0.0001f));
            }
        }

        [Test]
        public void FourActions_PlacesInnerPairAboveMatchingExtremes()
        {
            float[] lifts = LiftsFor(4);

            Assert.That(lifts[0], Is.EqualTo(lifts[3]).Within(0.0001f));
            Assert.That(lifts[1], Is.EqualTo(lifts[2]).Within(0.0001f));
            Assert.That(lifts[1], Is.GreaterThan(lifts[0]));
        }

        [Test]
        public void FiveActions_PlacesCenterAboveInnerPairAndExtremes()
        {
            float[] lifts = LiftsFor(5);

            Assert.That(lifts[0], Is.EqualTo(lifts[4]).Within(0.0001f));
            Assert.That(lifts[1], Is.EqualTo(lifts[3]).Within(0.0001f));
            Assert.That(lifts[1], Is.GreaterThan(lifts[0]));
            Assert.That(lifts[2], Is.GreaterThan(lifts[1]));
        }

        [Test]
        public void FourActions_RightEdgeBias_UsesCenteredCoordinates()
        {
            RectTransform[] slots = CreateSlots(1f);

            try
            {
				RectTransform[] centered = CreateSlots(0f);
				try
				{
					for (int index = 0; index < slots.Length; index++)
					{
						Assert.That(slots[index].anchorMin, Is.EqualTo(centered[index].anchorMin));
						Assert.That(slots[index].anchorMax, Is.EqualTo(centered[index].anchorMax));
						Assert.That(slots[index].offsetMin, Is.EqualTo(centered[index].offsetMin));
					}
				}
				finally
				{
					DestroySlots(centered);
				}
            }
            finally
            {
                DestroySlots(slots);
            }
        }

        [Test]
        public void FourActions_LeftAndRightBias_UseSameCenteredCoordinates()
        {
            RectTransform[] right = CreateSlots(1f);
            RectTransform[] left = CreateSlots(-1f);

            try
            {
                for (int index = 0; index < right.Length; index++)
                {
					Assert.That(left[index].anchorMin, Is.EqualTo(right[index].anchorMin));
					Assert.That(left[index].anchorMax, Is.EqualTo(right[index].anchorMax));
					Assert.That(left[index].offsetMin, Is.EqualTo(right[index].offsetMin));
                }
            }
            finally
            {
                DestroySlots(right);
                DestroySlots(left);
            }
        }

        private static RectTransform[] CreateSlots(float edgeBias)
        {
            Assert.That(SetTurnActionSlot, Is.Not.Null);
            var slots = new RectTransform[4];
            for (int index = 0; index < slots.Length; index++)
            {
                var gameObject = new GameObject(
                    "Turn Action Slot Test",
                    typeof(RectTransform),
                    typeof(CanvasRenderer),
                    typeof(Image),
                    typeof(Button));
                Button button = gameObject.GetComponent<Button>();
                SetTurnActionSlot.Invoke(null, new object[] { button, index, 4, edgeBias });
                slots[index] = gameObject.GetComponent<RectTransform>();
            }
            return slots;
        }

        private static void DestroySlots(RectTransform[] slots)
        {
            foreach (RectTransform slot in slots)
            {
                if (slot != null)
                    Object.DestroyImmediate(slot.gameObject);
            }
        }

        private static float[] LiftsFor(int count, float edgeBias = 0f)
        {
            Assert.That(CalculateVerticalLift, Is.Not.Null);
            var lifts = new float[count];
            for (int index = 0; index < count; index++)
                lifts[index] = (float)CalculateVerticalLift.Invoke(null, new object[] { index, count, edgeBias });
            return lifts;
        }
    }
}
