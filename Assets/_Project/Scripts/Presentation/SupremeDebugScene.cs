using System.Collections;
using System.Collections.Generic;
using System.Linq;
using AccardND.Battlefield;
using AccardND.GameCore;
using AccardND.GameData;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AccardND.Presentation
{
    /// <summary>Banco prova che usa direttamente i VFX condivisi da campagna e PvP.</summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class SupremeDebugScene : MonoBehaviour
    {
        [SerializeField] private CardDefinition[] classCards;
        private readonly List<PrototypeCardView> targets = new();
        private BattlePresentationAnimationPlayer animations;
        private BattleSfxPlayer sfx;
        private PrototypeCardView caster;
        private RectTransform allyRoot, targetRoot, manaIcon;
        private GameConfiguration configuration;
        private Coroutine running;

        private void Awake()
        {
            RemoveForeignBattleCanvases();
            EnsureEventSystem();
            configuration = Resources.Load<GameConfiguration>("GameConfiguration") ?? ScriptableObject.CreateInstance<GameConfiguration>();
            BuildUi();
            animations = gameObject.AddComponent<BattlePresentationAnimationPlayer>();
            AudioSource sceneAudio = GetComponent<AudioSource>();
            if (sceneAudio == null)
                sceneAudio = gameObject.AddComponent<AudioSource>();
            sceneAudio.playOnAwake = false;
            sceneAudio.spatialBlend = 0f;
            sfx = new BattleSfxPlayer();
            sfx.Initialize(transform, "Supreme Debug SFX");
            ShowClass(HeroClass.Hunter);
        }

        private void BuildUi()
        {
            Canvas canvas = new GameObject("Supreme Debug Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2000;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            Image bg = Image("Battle Background", canvas.transform, Color.white); Stretch(bg.rectTransform);
            bg.sprite = Resources.Load<Sprite>("Backgrounds/bg_arena_1");
            bg.preserveAspect = false;
            Image shade = Image("Arena Shade", canvas.transform, new Color(.006f,.012f,.028f,.52f)); Stretch(shade.rectTransform);
            Text title = Text("Title", canvas.transform, 38, FontStyle.Bold); title.text = "SUPREME — DEBUG PARTITA";
            Set(title.rectTransform, new(.04f,.925f), new(.96f,.985f));

            RectTransform buttons = Rect("Class Buttons", canvas.transform); Set(buttons, new(.04f,.025f), new(.96f,.105f));
            HeroClass[] classes = { HeroClass.Assassin, HeroClass.Warrior, HeroClass.Mage, HeroClass.Paladin, HeroClass.Rogue, HeroClass.Hunter, HeroClass.Barbarian, HeroClass.Necromancer, HeroClass.Priest };
            for (int i=0;i<classes.Length;i++) { HeroClass c=classes[i]; Button b=Button(c.ToString().ToUpperInvariant(), buttons); Set(b.GetComponent<RectTransform>(), new(i/9f+.004f,0), new((i+1)/9f-.004f,1)); b.onClick.AddListener(()=>ShowClass(c)); }
            Text enemyLabel=Text("Enemy Label",canvas.transform,21,FontStyle.Bold); enemyLabel.text="FORMAZIONE AVVERSARIA"; Set(enemyLabel.rectTransform,new(.24f,.84f),new(.76f,.90f));
            targetRoot = Rect("Enemy Formation", canvas.transform); Set(targetRoot, new(.06f,.55f), new(.94f,.84f)); ConfigureFormationRow(targetRoot);
            Image divider=Image("Battle Divider",canvas.transform,new Color(1f,.72f,.2f,.72f)); Set(divider.rectTransform,new(.29f,.49f),new(.71f,.495f));
            Text playerLabel=Text("Player Label",canvas.transform,21,FontStyle.Bold); playerLabel.text="LA TUA FORMAZIONE"; Set(playerLabel.rectTransform,new(.24f,.43f),new(.76f,.49f));
            allyRoot = Rect("Player Formation", canvas.transform); Set(allyRoot, new(.06f,.13f), new(.94f,.43f)); ConfigureFormationRow(allyRoot);
            manaIcon = Image("Paladin Mana Icon", canvas.transform, Color.white).rectTransform; manaIcon.GetComponent<Image>().sprite=Resources.Load<Sprite>("UI/mana_icon"); manaIcon.GetComponent<Image>().preserveAspect=true; Set(manaIcon,new(.79f,.39f),new(.86f,.51f)); manaIcon.gameObject.SetActive(false);
        }

        private void ShowClass(HeroClass heroClass)
        {
            if (running != null) StopCoroutine(running);
            Clear(allyRoot); Clear(targetRoot); manaIcon.gameObject.SetActive(heroClass==HeroClass.Paladin);
            CardDefinition definition = classCards?.FirstOrDefault(x=>x!=null && x.HeroClass==heroClass);
            if (definition==null) { Debug.LogError($"SupremeDebugScene: carta mancante per {heroClass}."); return; }
            CardDefinition[] allies = classCards.Where(x=>x!=null && x!=definition).Take(2).Prepend(definition).ToArray();
            for(int i=0;i<3;i++) { PrototypeCardView v=PrototypeCardView.CreateBattlefieldPreview(allyRoot,allies[i%allies.Length],configuration); if(i==0) caster=v; }
            targets.Clear();
            CardDefinition[] enemies=classCards.Where(x=>x!=null && x.HeroClass!=heroClass).Reverse().Take(3).ToArray();
            for(int i=0;i<3;i++) { PrototypeCardView v=PrototypeCardView.CreateBattlefieldPreview(targetRoot,enemies[i%enemies.Length],configuration); targets.Add(v); }
            running=StartCoroutine(Play(heroClass));
        }

        private IEnumerator Play(HeroClass c)
        {
            caster.PlaySupremeActionCallout();
            switch(c)
            {
                case HeroClass.Hunter: yield return animations.PlayHunterVolleySupreme(caster,targets,new[]{true,true,true},()=>sfx.PlayClassAbility(HeroClass.Hunter)); break;
                case HeroClass.Mage: sfx.PlayMageSupreme(); yield return animations.PlayMageFireballSupreme(caster,targets); break;
                case HeroClass.Barbarian: sfx.PlayBarbarianSupreme(); animations.StartCoroutine(animations.PlayBarbarianSupreme(caster)); foreach(var t in targets) animations.StartCoroutine(animations.PlayBarbarianFury(t)); break;
                case HeroClass.Paladin: yield return animations.PlayPaladinSupremePulse(caster,manaIcon,false,3); break;
                case HeroClass.Warrior: sfx.PlayWarriorSupreme(); WarriorSupremeVfx.Activate(caster,2); break;
                case HeroClass.Assassin: sfx.PlayAssassinSupreme(); caster.SetAssassinSilverFilm(true); break;
                case HeroClass.Rogue: yield return animations.PlayRogueSupremeBlackHand(caster,targets[0]); break;
                case HeroClass.Necromancer: sfx.PlayNecromancerSupreme(); yield return NecromancerMinionVfx.Summon(caster.RectTransform,true); break;
                case HeroClass.Priest: sfx.PlayPriestSupreme(); yield return PriestSupremeVfx.Play(caster.RectTransform,targets); break;
            }
            running=null;
        }

        private static void Clear(Transform root){ for(int i=root.childCount-1;i>=0;i--) Destroy(root.GetChild(i).gameObject); }
        private static void ConfigureFormationRow(RectTransform root){HorizontalLayoutGroup row=root.gameObject.AddComponent<HorizontalLayoutGroup>();row.spacing=34f;row.childAlignment=TextAnchor.MiddleCenter;row.childControlWidth=true;row.childControlHeight=true;row.childForceExpandWidth=true;row.childForceExpandHeight=true;row.padding=new RectOffset(10,10,0,0);}
        private static void RemoveForeignBattleCanvases(){foreach(Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None)){if(canvas!=null&&canvas.name=="Battle Canvas")Destroy(canvas.gameObject);}}
        private static RectTransform Rect(string n,Transform p){var r=new GameObject(n,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);return r;}
        private static Image Image(string n,Transform p,Color c){var x=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image)).GetComponent<Image>();x.transform.SetParent(p,false);x.color=c;return x;}
        private static Text Text(string n,Transform p,int size,FontStyle style){var t=new GameObject(n,typeof(RectTransform),typeof(CanvasRenderer),typeof(Text)).GetComponent<Text>();t.transform.SetParent(p,false);t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");t.fontSize=size;t.fontStyle=style;t.alignment=TextAnchor.MiddleCenter;t.color=Color.white;t.resizeTextForBestFit=true;EditableRuntimeText.Bind(t);return t;}
        private static Button Button(string label,Transform p){Image i=Image(label,p,new Color(.12f,.19f,.31f,.98f));Button b=i.gameObject.AddComponent<Button>();b.targetGraphic=i;Text t=Text("Label",i.transform,22,FontStyle.Bold);t.text=label;Stretch(t.rectTransform,5);return b;}
        private static void Set(RectTransform r,Vector2 min,Vector2 max){r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        private static void Stretch(RectTransform r,float pad=0){Set(r,Vector2.zero,Vector2.one);r.offsetMin=new(pad,pad);r.offsetMax=new(-pad,-pad);}
        private static void EnsureEventSystem(){if(FindAnyObjectByType<EventSystem>()!=null)return;var e=new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));e.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();}
    }
}
