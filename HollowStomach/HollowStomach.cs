using System;
using System.Collections.Generic;
using GlobalEnums;
using Modding;
using UnityEngine;

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
	 *     - picking up any Geo gives you 1/3 SOUL
	 *	   - picking up a random Purple Geo prevents SOUL from draining for N seconds and
	 *	     gives you a speed boost
	 *     - being near a bench increases your SOUL to full at a rate of N/s
	 *     - hitting a boss (hardcoded) drops geo
	 *     - works with all custom knight skins
	 */
	public class HollowStomach : Mod
	{
	 	public override string GetVersion() => "1.1.0";
	 	public HollowStomach() : base("Hollow Stomach") { }
	 	public float timer_SoulDrain = 0;
		private float soulDrainTimer = 0.3030303f; // interval to drain soul
		private int hungerLevel; // how much soul to drain 
		public float timer_SoulGain = 0;
		private int minSoul; // minimum soul at resting
		private readonly float soulGainTimer = 0.101010101f;
		private readonly int minSoulGainInterval = 1;
		public float timer_TakeDamage = 0;
		private readonly float healthTimer = 2f; // health drain interval
		public float timer_RegainSoul = 0;
        private int bossGeoDropChance; // boss drops geo every N hits
		private int soulPerGeo;
        private int hitCounter = 0;
        private bool shouldDamage = false;
		// purple cherries give you a speed boost and prevent soul from draining
		private int purpleCherry = 1000; // rate at which purple cherries drop
		private float purpleCherryTimegain = 6.0f;
		private readonly float defaultRunSpeed = 8.3f;
		private readonly float cherrySpeedBoost = 1.5f;
		public GameObject _smallGeo;
		public GameObject SmallGeo => UnityEngine.Object.Instantiate(_smallGeo);

		private class MyGlobalSettings : Modding.ModSettings
		{
			public bool starvationMode = false;
		}

		private MyGlobalSettings Settings = new MyGlobalSettings();
		public override Modding.ModSettings GlobalSettings
		{
			get => Settings;
			set => Settings = (MyGlobalSettings)value;
		}

		private readonly bool debug = false; //TODO: TURN THIS OFF

		private readonly List<String> dreams = new List<String> { "White_Palace_01", "White_Palace_02", "White_Palace_03_hub", "White_Palace_04", "White_Palace_05", "White_Palace_06", "White_Palace_07", "White_Palace_08", "White_Palace_09", "White_Palace_10", "White_Palace_11", "White_Palace_12", "White_Palace_13", "White_Palace_14", "White_Palace_15", "White_Palace_16", "White_Palace_17", "White_Palace_18", "White_Palace_19", "White_Palace_20", "Dream_Abyss", "Dream_Room_Believer_Shrine", "Dream_Backer_Shrine", "Dream_Guardian_Monomon", "Dream_Guardian_Lurien", "Dream_Guardian_Hegemol", "Dream_Mighty_Zote", "Dream_04_White_Defender", "Dream_02_Mage_Lord", "Dream_01_False_Knight", "Dream_Nailcollection", "Dream_03_Infected_Knight", "Room_Ouiji", "GG_Unlock_Wastes", "GG_Blue_Room", "GG_Workshop", "GG_Land_of_Storms", "GG_Atrium", "GG_Atrium_Roof", "GG_Engine", "GG_Engine_Prime", "GG_Unn", "GG_Engine_Root", "GG_Wyrm", "GG_Spa", "Abyss_06_Core", "Abyss_08", "Abyss_15", "Abyss_16", "Abyss_09", "Abyss_Lighthouse_Room", "Abyss_10" };
		// the abyss was softlocking you so this is the only real workaround, it's not ideal but i have no better ideas

		private readonly List<String> bossNames = new List<String> { "Dream Mage Lord", "Dung Defender", "Fluke Mother", "Ghost Warrior Galien", "Ghost Warrior Hu", "Ghost Warrior Markoth", "Ghost Warrior Marmu", "Ghost Warrior No Eyes", "Ghost Warrior Slug", "Ghost Warrior Xero", "Giant Buzzer Col", "Giant Fly", "Grey Prince", "Grimm Boss", "Head", "Hive Knight", "Hornet Boss 1", "Hornet Boss 2", "Infected Knight", "Jar Collector", "Jellyfish GG(Clone)", "Lancer", "Lobster", "Lost Kin", "Mage Knight", "Mage Lord", "Mantis Lord", "Mantis Lord S1", "Mantis Lord S2", "Mantis Traitor Lord", "Mawlek Body", "Mega Fat Bee", "Mega Fat Bee (1)", "Mega Zombie Beam Miner (1)", "Mimic Spider", "Nightmare Grimm Boss", "Radiance", "White Defender", "Zombie Beam Miner Rematch", "Hollow Knight Boss", "Mega Jellyfish" };

		private readonly String hollowKnightArena = "Room_Final_Boss_Core";
		private readonly List<String> hollowKnight = new List<String> { "Idle", "Head", "Slash Antic", "Stun Fall", "Dstab Damage", "Counter", "Counter Antic", "Dash Antic", "Stun", "Roar Antic", "SelfStab Antic", "SmallShot", "Puppet Down", "Puppet Up", "ChestShot", "Dream Enter", "Hornet Collapse", "Collapse", "Boss Corpse" };

		private readonly String mossyArena = "Fungus1_29";
		private readonly List<String> mossyPet = new List<String> { "Charge Hit", "Leap Hit", "Burrow Hit" };

		private readonly String watcherKnightArena = "Ruins2_03";
		private readonly List<String> watcherKnights = new List<String> { "Black Knight 1", "Black Knight 2", "Black Knight 3", "Black Knight 4", "Black Knight 5", "Black Knight 6" };

		private readonly List<String> mageLordArena = new List<String> { "Ruins_24", "Dream_02_Mage_Lord" };
		private readonly List<String> mageLord = new List<String> { "Mage Lord", "Dream Mage Lord", "Wound Box", "Quake Box", "Mage Lord Phase2", "Head Box", "Dream Mage Lord Phase2" };

		private readonly List<String> falseKnightArena = new List<String> { "Crossroads_10", "Dream_01_False_Knight" };
		private readonly String uumuuArena = "Fungus3_archive_02";
		private readonly List<String> baldurRooms = new List<String> { "Crossroads_ShamanTemple", "Crossroads_11_alt", "Fungus1_28"};
		private readonly List<String> smoldurs = new List<String> { "Spawn Roller v2", "Spawn Roller v2(Clone)" };
		private readonly String kingsPass = "Tutorial_01";

		// i want to refill soul on certain pickups, to avoid softlocking players there. this holds the bools to check against
		private readonly List<String> pickups = new List<String> { "hasSuperDash", "hasShadowDash", "hasAcidArmor", "hasDoubleJump", "hasKingsBrand", "hasSpell" };
		public override void Initialize()
		{
			Log("Hollow Stomach v." + GetVersion());
			ModHooks.Instance.SoulGainHook += NoSoul;
			ModHooks.Instance.HeroUpdateHook += DrainSoul; // drain the soul
			ModHooks.Instance.HeroUpdateHook += Starve;    // die if you don't eat
			ModHooks.Instance.HeroUpdateHook += checkNearBench; // are we near a bench
			ModHooks.Instance.SlashHitHook += shakeDown;
			On.GeoCounter.AddGeo += getCherry;
			getPrefab();
			Log("Hollow Stomach: " + (Settings.starvationMode ? "Starvation Mode" : "Normal Mode" ));

			purpleCherry = (Settings.starvationMode ? 250 : 1000);
			purpleCherryTimegain = (Settings.starvationMode ? 3.0f : 6.0f);
			soulDrainTimer = (Settings.starvationMode ? 0.4040404f : 0.3030303f);
			hungerLevel = (Settings.starvationMode ? 2 : 1);
			minSoul = (Settings.starvationMode ? 99 : 99);
			bossGeoDropChance = (Settings.starvationMode ? 9 : 6);
			soulPerGeo = (Settings.starvationMode ? 22 : 33);
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
				SpeedMax = 45f,
				AngleMin = 80f,
				AngleMax = 115f
			};
			FlingUtils.SpawnAndFling(flingConfig, otherCollider.gameObject.transform, new Vector3(0f, 0f, 0f));
			smallPrefab.SetActive(false);
		}

		private void shakeDown(Collider2D otherCollider, GameObject gameObject)
		{
			Log("HIT: " + otherCollider.gameObject.name);
			bool getChange = false;
			int maxChance = (Settings.starvationMode ? bossGeoDropChance * 2 : bossGeoDropChance);
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
				maxChance = 1;
			}
			else if (GameManager.instance.sceneName == watcherKnightArena && watcherKnights.Contains(otherCollider.gameObject.name))
			{
				getChange = true;
			}
			// WHY
			else if (mageLordArena.Contains(GameManager.instance.sceneName) && mageLord.Contains(otherCollider.gameObject.name))
			{
				getChange = true;
			}
			else if (GameManager.instance.sceneName == uumuuArena && otherCollider.gameObject.name == "Mega Jellyfish")
			{
				getChange = true;
				maxChance = (Settings.starvationMode ? 3 : 1);
			}
			// avoid softlocks at lurien and monomon
			else if (otherCollider.gameObject.name == "Dreamer NPC")
			{
				PlayerData.instance.AddMPCharge(99);
				GameCameras.instance.soulOrbFSM.SendEvent("MP GAIN");
			}
			else
			{
				getChange = bossNames.Contains(otherCollider.gameObject.name);
			}
			if(getChange)
			{
				hitCounter++;
				if (hitCounter % maxChance == 0)
				{
					pocketChange(otherCollider);
					hitCounter = 0;
				}
			}
		}

		private void getCherry(On.GeoCounter.orig_AddGeo orig, GeoCounter self, int geo)
		{
			if(UnityEngine.Random.Range(0, purpleCherry) <= (geo > 26 ? 25 : geo))
			{
				timer_SoulDrain = -1 * purpleCherryTimegain;
				HeroController.instance.RUN_SPEED = cherrySpeedBoost * defaultRunSpeed;
				PlayerData.instance.AddMPCharge(soulPerGeo);
			}
			// technically this runs every time geo is added (i.e., shade collection)
			// but thats not really a problem i care about, its fine
			PlayerData.instance.AddMPCharge(soulPerGeo);
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
					 (HeroController.instance.controlReqlinquished && !HeroController.instance.cState.superDashing && !HeroController.instance.cState.superDashOnWall )
					 || (GameManager.instance.sceneName == kingsPass && PlayerData.instance.geo == 0)
				);
		}

		private bool inDreamWorld()
		{
			return dreams.Contains(GameManager.instance.sceneName);
		}

		private void checkNearBench()
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
						HeroController.instance.RUN_SPEED = defaultRunSpeed;
						timer_SoulDrain -= soulDrainTimer;
					}
					if (HeroController.instance.RUN_SPEED > defaultRunSpeed)
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
