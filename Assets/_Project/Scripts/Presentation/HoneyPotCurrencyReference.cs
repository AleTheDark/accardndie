using UnityEngine;

namespace AccardND.Presentation
{
[CreateAssetMenu(menuName = "AccardND/UI/Honey Pot Currency Reference")]
public sealed class HoneyPotCurrencyReference : ScriptableObject
{
	[SerializeField] private Sprite sprite;

	public Sprite Sprite => sprite;
}
}
