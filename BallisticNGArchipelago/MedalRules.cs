using System;
using NgSp;
namespace BallisticNGArchipelago {
 public static class MedalRules {
  public static float Threshold(NgCampaignEvent.Progress p,int tier) {
   switch(tier) { case 1:return p.BronzeValue;case 2:return p.SilverValue;case 3:return p.GoldValue;case 4:return p.PlatinumValue;default:return -1f; }
  }
  public static NgAward Position(int place) {
   NgAward award=place>=1 && place<=3 ? (NgAward)(4-place):NgAward.None;
   return Hard(award);
  }
  public static NgAward Hard(NgAward award) {
   return NgCampaign.HardEnabled ? (award==NgAward.Gold ? NgAward.Platinum:NgAward.None):award;
  }
  public static NgAward Zones(NgCampaignEvent.Progress p,float multiplier,int zone) {
   for(int tier=4;tier>=1;tier--) { float value=Threshold(p,tier);if(value>=0f && zone>=value*multiplier) return (NgAward)tier; }
   return NgAward.None;
  }
  public static string Milestone(string key,int tier) {
   switch(tier) { case 1:return key+":bronze";case 2:return key+":silver";case 3:return key+":gold";default:return null; }
  }
 }
}
