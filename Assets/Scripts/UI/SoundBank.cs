using UnityEngine;

namespace CardBattler.UI
{
    // Kenney sound clips referenced by Resources/UI/Sounds.asset (the clips stay in ThirdParty).
    [CreateAssetMenu(menuName = "Card Battler/Sound Bank")]
    public class SoundBank : ScriptableObject
    {
        public AudioClip click, hover, back, open, error, card, scrap, hit, heavyHit, broken, armor, repair, win, lose, turn;
    }
}
