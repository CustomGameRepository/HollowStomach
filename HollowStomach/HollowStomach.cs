using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GlobalEnums;
using Modding;
using HutongGames.PlayMaker;
using UnityEngine;
using Random = System.Random;

namespace HollowStomach
{
	/*
	 * Make Hollow Knight more like Hungry Knight
	 * Behavior:
	 *     - hitting enemies does not give SOUL
	 *     - SOUL drains at a rate of N/sec, starting when you first pick up Geo
	 *     - if you are at zero SOUL, you take 1 mask every N seconds
	 *     - if you're in a dream sequence, SOUL doesn't drain but you're still subject to 
	 *       starvation
	 *     - being within some distance of a bench doesn't cause your SOUL to drain
	 *     - picking up any Geo resets your SOUL to full 
	 *     - being near a bench increases your SOUL to full at a rate of N/s
	 *     - hitting a boss (hardcoded) drops geo
	 */
	public class HollowStomach : Mod
    {
        public override string GetVersion() => "0.7-alpha";
		public HollowStomach() : base("Hollow Stomach") { }
		public float timer_SoulDrain = 0;
		public float timer_SoulGain = 0;
		public float timer_TakeDamage = 0;
		private float soulDrainTimer = 0.4f; // interval to drain soul
		public int hungerLevel = 2; // how much soul to drain 
		private float soulGainTimer = 0.25f;
		private float healthTimer = 2f; // health drain interval
		private int minSoul = 99; // minimum soul at resting
		private int minSoulGainInterval = 3;
		private int bossGeoDropChance = 6; // 1/N chance for boss to drop geo
		private bool shouldDamage = false;
		public GameObject _smallGeo;
		public GameObject SmallGeo => UnityEngine.Object.Instantiate(_smallGeo);

		private bool debug = false; //TODO: TURN THIS OFF

		private List<String> dreams = new List<String> {"White_Palace_01", "White_Palace_02", "White_Palace_03_hub", "White_Palace_04", "White_Palace_05", "White_Palace_06", "White_Palace_07", "White_Palace_08", "White_Palace_09", "White_Palace_10", "White_Palace_11", "White_Palace_12", "White_Palace_13", "White_Palace_14", "White_Palace_15", "White_Palace_16", "White_Palace_17", "White_Palace_18", "White_Palace_19", "White_Palace_20", "Dream_Abyss", "Dream_Room_Believer_Shrine", "Dream_Backer_Shrine", "Dream_Guardian_Monomon", "Dream_Guardian_Lurien", "Dream_Guardian_Hegemol", "Dream_Mighty_Zote", "Dream_04_White_Defender", "Dream_02_Mage_Lord", "Dream_01_False_Knight", "Dream_Nailcollection", "Dream_03_Infected_Knight", "Room_Ouiji", "GG_Unlock_Wastes", "GG_Blue_Room", "GG_Workshop", "GG_Land_of_Storms", "GG_Atrium", "GG_Atrium_Roof", "GG_Engine", "GG_Engine_Prime", "GG_Unn", "GG_Engine_Root", "GG_Wyrm", "GG_Spa" };

		private List<String> bossNames = new List<String> { "Dream Mage Lord", "Dung Defender", "Fluke Mother", "Ghost Warrior Galien", "Ghost Warrior Hu", "Ghost Warrior Markoth", "Ghost Warrior Marmu", "Ghost Warrior No Eyes", "Ghost Warrior Slug", "Ghost Warrior Xero", "Giant Buzzer Col", "Giant Fly", "Grey Prince", "Grimm Boss", "Head", "Hive Knight", "Hornet Boss 1", "Hornet Boss 2", "Infected Knight", "Jar Collector", "Jellyfish GG(Clone)", "Lancer", "Lobster", "Lost Kin", "Mage Knight", "Mage Lord", "Mantis Lord", "Mantis Lord S1", "Mantis Lord S2", "Mantis Traitor Lord", "Mawlek Body", "Mega Fat Bee", "Mega Fat Bee (1)", "Mega Zombie Beam Miner (1)", "Mimic Spider", "Nightmare Grimm Boss", "Radiance", "White Defender", "Zombie Beam Miner Rematch", "Hollow Knight Boss", "Mega Jellyfish" };

		private String hollowKnightArena = "Room_Final_Boss_Core";
		private List<String> hollowKnight = new List<String> { "Idle", "Head", "Slash Antic", "Stun Fall", "Dstab Damage", "Counter", "Counter Antic", "Dash Antic", "Stun", "Roar Antic", "SelfStab Antic", "SmallShot", "Puppet Down", "Puppet Up", "ChestShot", "Dream Enter", "Hornet Collapse", "Collapse", "Boss Corpse" };

		private String mossyArena = "Fungus1_29";
		private List<String> mossyPet = new List<String> { "Charge Hit", "Leap Hit", "Burrow Hit" };

		private String watcherKnightArena = "Ruins2_03";
		private List<String> watcherKnights = new List<String> { "Black Knight 1", "Black Knight 2", "Black Knight 3", "Black Knight 4", "Black Knight 5", "Black Knight 6" };

		private List<String> falseKnightArena = new List<String> { "Crossroads_10", "Dream_01_False_Knight" };
		private List<String> baldurRooms = new List<String> { "Crossroads_ShamanTemple", "Crossroads_11_alt", "Fungus1_28"};
		private List<String> smoldurs = new List<String> { "Spawn Roller v2", "Spawn Roller v2(Clone)" };
		private String kingsPass = "Tutorial_01";
		public override void Initialize()
        {
            Log("Hollow Stomach v." + GetVersion());
			ModHooks.Instance.SoulGainHook += NoSoul;
			ModHooks.Instance.HeroUpdateHook += DrainSoul;
			ModHooks.Instance.HeroUpdateHook += Starve;
			ModHooks.Instance.HeroUpdateHook += shouldRefresh;
			On.GeoCounter.AddGeo += getCherry;
			ModHooks.Instance.SlashHitHook += shakeDown;
			ModHooks.Instance.NewGameHook += HandleNewGameSoul;
			//ModHooks.Instance.SavegameLoadHook += HandleNewGameSoul;
			getPrefab();
		}


        public void getPrefab()
        {
			// this is very bad but it will work
			Resources.LoadAll<GameObject>("");
			foreach (GameObject i in Resources.FindObjectsOfTypeAll<GameObject>())
            {
				if(i.name == "Geo Small")
                {
					_smallGeo = i;
				}
            }
        }
		private void HandleNewGameSoul()
        {
			// when the game starts, i want to make sure you have full soul
			HeroController.instance.SetMPCharge(99);
            GameCameras.instance.soulOrbFSM.SendEvent("MP GAIN");
        }

		private void pocketChange(Collider2D otherCollider)
        {
			// thanks randomizer3.0
			GameObject smallPrefab = _smallGeo;
			UnityEngine.Object.Destroy(smallPrefab.Spawn());
			smallPrefab.SetActive(true);
			FlingUtils.Config flingConfig = new FlingUtils.Config
			{
				Prefab = smallPrefab,
				AmountMin = 1,
				AmountMax = 1,
				SpeedMin = 15f,
				SpeedMax = 30f,
				AngleMin = 80f,
				AngleMax = 115f
			};
			FlingUtils.SpawnAndFling(flingConfig, otherCollider.gameObject.transform, new Vector3(0f, 0f, 0f));
			smallPrefab.SetActive(false);
		}

		private void shakeDown(Collider2D otherCollider, GameObject gameObject)
        {
			bool getChange = false;
			int maxChance = bossGeoDropChance;
			// why are the hollow knight attacks different game objects
			// i hate this
			if(GameManager.instance.sceneName == hollowKnightArena && hollowKnight.Contains(otherCollider.gameObject.name))
            {
				getChange = true;
            }
			else if (GameManager.instance.sceneName == mossyArena && mossyPet.Contains(otherCollider.gameObject.name))
			{
				getChange = true;
			}
			else if (falseKnightArena.Contains(GameManager.instance.sceneName) && otherCollider.gameObject.name == "Head")
			{
				getChange = true;
			}
			else if (baldurRooms.Contains(GameManager.instance.sceneName) && smoldurs.Contains(otherCollider.gameObject.name))
			{
				getChange = true;
				maxChance = 0;
			}
			else if (GameManager.instance.sceneName == watcherKnightArena && watcherKnights.Contains(otherCollider.gameObject.name))
            {
				getChange = true;
			}
			else
            {
				getChange = bossNames.Contains(otherCollider.gameObject.name);
            }
			if(getChange)
            {
				int r = UnityEngine.Random.Range(0, maxChance + 1);
				//Log("Hit: " + r);
				if (r == 0)
                {
					pocketChange(otherCollider);
                }
            }
		}

		private void getCherry(On.GeoCounter.orig_AddGeo orig, GeoCounter self, int geo)
        {
			PlayerData.instance.AddMPCharge(99);
			GameCameras.instance.soulOrbFSM.SendEvent("MP GAIN");
			if (PlayerData.instance.GetInt("MPReserveMax") > 0)
            {
				GameCameras.instance.soulVesselFSM.SendEvent("MP GAIN");
			}
			orig(self, geo);
        }

        private bool shouldDrain()
        {
		 	// check if they're in a state in which we don't drain soul
			// including if they're in king's pass with no geo, which i'm
			// assuming means it's a new file. this is possible to abuse,
			// but it's king's pass, what's the worst that can happen?
			return !(GameManager.instance.IsCinematicScene() ||
				     GameManager.instance.IsNonGameplayScene() ||
				     HeroController.instance.cState.dead ||
					 HeroController.instance.cState.hazardDeath ||
					 HeroController.instance.cState.hazardRespawning ||
					 HeroController.instance.cState.transitioning ||
					 HeroController.instance.cState.invulnerable ||
					 HeroController.instance.cState.nearBench ||
					 HeroController.instance.cState.recoiling ||
					 HeroController.instance.cState.recoilingLeft ||
					 HeroController.instance.cState.recoilingRight ||
					 HeroController.instance.cState.isPaused ||
					 (HeroController.instance.controlReqlinquished && (!HeroController.instance.cState.superDashing || !HeroController.instance.cState.superDashOnWall ))
					 || (GameManager.instance.sceneName == kingsPass && PlayerData.instance.geo == 0)
					 );
		}

		private bool inDreamWorld()
        {
			//Log("SCENE: " + GameManager.instance.sceneName + " " + (dreams.Contains(GameManager.instance.sceneName) ? "DREAM" : "NO DREAM") );
			return dreams.Contains(GameManager.instance.sceneName);
        }
        private void shouldRefresh()
        {
			if (HeroController.instance.cState.nearBench)
			{
				timer_SoulGain += Time.deltaTime;
				while (timer_SoulGain >= soulGainTimer)
                {
					if (PlayerData.instance.GetInt("MPCharge") <= minSoul)
					{
						PlayerData.instance.AddMPCharge(minSoulGainInterval);
						GameCameras.instance.soulOrbFSM.SendEvent("MP GAIN SPA");
					}
					timer_SoulGain -= soulGainTimer;
				}
			}
        }

		public bool vesselDrained()
        {
			if (PlayerData.instance.GetInt("MPReserveMax") > 0)
			{
				if (PlayerData.instance.GetInt("MPReserve") > hungerLevel)
				{
					PlayerData.instance.TakeReserveMP(hungerLevel);
					GameCameras.instance.soulVesselFSM.SendEvent("MP RESERVE DOWN");
					shouldDamage = false;
					return false;
				}
				else
				{
					int overflow = hungerLevel - PlayerData.instance.GetInt("MPReserve");
					PlayerData.instance.TakeReserveMP(PlayerData.instance.GetInt("MPReserve"));
					PlayerData.instance.TakeMP(overflow);
					if(overflow < 0)
                    {
						GameCameras.instance.soulVesselFSM.SendEvent("MP RESERVE DOWN");
                    }
					GameCameras.instance.soulOrbFSM.SendEvent("MP DRAIN");
					shouldDamage = false;
					return true;
				}
			}
			return true;
        }

		public void DrainSoul()
		{
			if (!inDreamWorld() && !debug)
            {
				if (shouldDrain())
				{
					timer_SoulDrain += Time.deltaTime;
					while (timer_SoulDrain >= soulDrainTimer)
					{
						if(vesselDrained())
                        {
							if (PlayerData.instance.GetInt("MPCharge") >= hungerLevel)
							{
								PlayerData.instance.TakeMP(hungerLevel);
								GameCameras.instance.soulOrbFSM.SendEvent("MP DRAIN");
								shouldDamage = false;
							}
							else
							{
								shouldDamage = true;
							}
                        }
						timer_SoulDrain -= soulDrainTimer;
					}
				}
            }
			else
            {
				if (PlayerData.instance.GetInt("MPReserveMax") > 0 && PlayerData.instance.GetInt("MPReserve") <= hungerLevel && PlayerData.instance.GetInt("MPCharge") < hungerLevel)
                {
					shouldDamage = true;
				}
				else
                {
					shouldDamage = false;
				}
			}
        }
		public void Starve()
		{
			if (shouldDamage && shouldDrain() && !debug)
            {
				timer_TakeDamage += Time.deltaTime;
				while (timer_TakeDamage >= healthTimer)
				{
					if (PlayerData.instance.GetInt("MPCharge") < hungerLevel && PlayerData.instance.GetInt("MPReserve") < hungerLevel)
					{
						HeroController.instance.TakeDamage(HeroController.instance.gameObject, CollisionSide.other, 1, 0);
					}
					timer_TakeDamage -= healthTimer;
				}
            }
		}

		private int NoSoul(int amount)
        {
			return (debug ? amount : 0);
        }

	}
}
