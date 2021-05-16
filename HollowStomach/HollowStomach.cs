using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using GlobalEnums;
using Modding;
using HutongGames.PlayMaker;
using UnityEngine;

namespace HollowStomach
{
	/*
	 * Make Hollow Knight more like Hungry Knight
	 * Behavior:
	 *     - hitting enemies does not give SOUL (done-ish)
	 *     - SOUL drains at a rate of N/sec (done)
	 *     - if you are at zero SOUL, you take 1 mask every N seconds (done)
	 *     - if you're in a dream sequence, SOUL doesn't drain but you're still subject to 
	 *       starvation (done)
	 *     - being within some distance of a bench doesn't cause your SOUL to drain (done)
	 *     - picking up any Geo resets your SOUL to full (HeroController.instance.SetMPCharge)
	 *     - being near a bench resets your SOUL to full
	 *     - hitting a boss (hardcoded) drops 1 geo (check bonfire)
	 */
	public class HollowStomach : Mod
    {
        public override string GetVersion() => "0.5-alpha";
		public HollowStomach() : base("Hollow Stomach") { }
		public float timer_SoulDrain = 0;
		public float timer_TakeDamage = 0;
		public int hungerLevel = 2; // how much soul to drain every 1/2 sec
		public float healthTimer = 3f; // health drain interval
		public bool shouldDamage = false;

		private List<String> dreams = new List<String> {"White_Palace_01", "White_Palace_02", "White_Palace_03_hub", "White_Palace_04", "White_Palace_05", "White_Palace_06", "White_Palace_07", "White_Palace_08", "White_Palace_09", "White_Palace_10", "White_Palace_11", "White_Palace_12", "White_Palace_13", "White_Palace_14", "White_Palace_15", "White_Palace_16", "White_Palace_17", "White_Palace_18", "White_Palace_19", "White_Palace_20", "Dream_Abyss", "Dream_Room_Believer_Shrine", "Dream_Backer_Shrine", "Dream_Guardian_Monomon", "Dream_Guardian_Lurien", "Dream_Guardian_Hegemol", "Dream_Mighty_Zote", "Dream_04_White_Defender", "Dream_02_Mage_Lord", "Dream_01_False_Knight", "Dream_Nailcollection", "Dream_03_Infected_Knight", "Room_Ouiji" };

        public override void Initialize()
        {
            Log("Hollow Stomach v." + GetVersion());
			ModHooks.Instance.SoulGainHook += NoSoul;
			ModHooks.Instance.HeroUpdateHook += DrainSoul;
			ModHooks.Instance.HeroUpdateHook += Starve;
            On.HeroController.OnCollisionEnter2D += getCherry;

        }

        private bool shouldDrain()
        {
			/*
		 	 * check if they're in a state in which we don't drain soul
			 */
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
					 HeroController.instance.cState.isPaused// ||
					 //HeroController.instance.controlReqlinquished //TC spellcheck your code pls
					);
		}

		private bool inDreamWorld()
        {
			//Log("SCENE: " + GameManager.instance.sceneName + " " + (dreams.Contains(GameManager.instance.sceneName) ? "DREAM" : "NO DREAM") );
			return dreams.Contains(GameManager.instance.sceneName);
        }

		private void getCherry(On.HeroController.orig_OnCollisionEnter2D orig, HeroController self, Collision2D collision)
        {
			if(false)
            {
				throw new NotImplementedException();
            }
			else
			{
				orig(self, collision);
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
					Log("1 - SHOULDNT DAMAGE");
					return false;
				}
				else
				{
					int overflow = hungerLevel - PlayerData.instance.GetInt("MPReserve");
					PlayerData.instance.TakeReserveMP(PlayerData.instance.GetInt("MPReserve"));
					PlayerData.instance.TakeMP(overflow);
					GameCameras.instance.soulVesselFSM.SendEvent("MP RESERVE DOWN");
					GameCameras.instance.soulOrbFSM.SendEvent("MP DRAIN");
					shouldDamage = false;
					Log("2 - SHOULDNT DAMAGE");
					return true;
				}
			}
			return true;
        }

		public void DrainSoul()
		{
			if (!inDreamWorld())
            {
				if (shouldDrain())
				{
					timer_SoulDrain += Time.deltaTime;
					while (timer_SoulDrain >= 0.5f)
					{
						if(vesselDrained())
                        {
							if (PlayerData.instance.GetInt("MPCharge") >= hungerLevel)
							{
								PlayerData.instance.TakeMP(hungerLevel);
								GameCameras.instance.soulOrbFSM.SendEvent("MP DRAIN");
								shouldDamage = false;
								Log("3 - SHOULDNT DAMAGE");
							}
							else
							{
								shouldDamage = true;
								Log("4 - SHOULD DAMAGE");
							}
                        }
						timer_SoulDrain -= 0.5f;
					}
				}
            }
			else
            {
				if (PlayerData.instance.GetInt("MPReserveMax") > 0 && PlayerData.instance.GetInt("MPReserve") <= hungerLevel && PlayerData.instance.GetInt("MPCharge") < hungerLevel)
                {
					shouldDamage = true;
					Log("SHOULD DAMAGE");
				}
				else
                {
					shouldDamage = false;
					Log("SHOULDNT DAMAGE");
				}
			}
        }
		public void Starve()
		{
			if (shouldDamage && shouldDrain())
            {
				Log("DAMAGING");
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
			return 0;
        }

	}
}
