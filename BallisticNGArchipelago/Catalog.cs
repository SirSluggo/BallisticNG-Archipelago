namespace BallisticNGArchipelago {
public sealed class EventSpec {
 public string Key, Campaign, Name, Levels, Speed, Mode; public int Group, Index; public bool Modern;
 public EventSpec(string key,string campaign,string name,int group,int index,string levels,string speed,bool modern,string mode) {
 Key=key;Campaign=campaign;Name=name;Group=group;Index=index;Levels=levels;Speed=speed;Modern=modern;Mode=mode; }
}
public static class Catalog {
 public const string Digest = "4c275f56fb0e213fbc894aa3db4d11d23b75f01e581726038216e47346745a59";
 public static string AccessItemName(string campaign) { switch(campaign) {
  case "Techno": return "Techno Invitational";
  case "Urban": return "Urban Invitational";
  case "Reflex": return "Reflex Invitational";
  case "Ascension": return "Ascension Invitational";
  case "Descension": return "Descension Invitational";
  case "Speed Freak": return "Speed Freak Invitational";
  case "Zen Zeus": return "Zen Zeus Invitational";
  case "Finale A": return "Finale A Entry";
  case "Finale B": return "Finale B Entry";
  case "Finale C": return "Finale C Entry";
  default:return campaign; } }
 public static string RequiredFinale(string key) {
  switch(key) {
   case "bng05:21591050": return "Finale A";
   case "bng05:21591051": return "Finale B";
   case "bng05:21591052": return "Finale C";
   default: return null;
  }
 }
 public static readonly string[] AccessNames = new string[] {"Cosmetic:Wyvern:Alt","Cosmetic:Wyvern:Neon","Cosmetic:Wyvern:Clean","Cosmetic:Wyvern:Stealth","Cosmetic:Hyperion:Alt","Cosmetic:Hyperion:Neon","Cosmetic:Hyperion:Clean","Cosmetic:Hyperion:Stealth","Cosmetic:Scorpio:Alt","Cosmetic:Scorpio:Neon","Cosmetic:Scorpio:Clean","Cosmetic:Scorpio:Stealth","Cosmetic:Omnicom:Alt","Cosmetic:Omnicom:Neon","Cosmetic:Omnicom:Clean","Cosmetic:Omnicom:Stealth","Cosmetic:Diavolt:Alt","Cosmetic:Diavolt:Neon","Cosmetic:Diavolt:Clean","Cosmetic:Diavolt:Stealth","Cosmetic:Nexus:Alt","Cosmetic:Nexus:Neon","Cosmetic:Nexus:Clean","Cosmetic:Nexus:Stealth","Cosmetic:Tenrai:Alt","Cosmetic:Tenrai:Neon","Cosmetic:Tenrai:Clean","Cosmetic:Tenrai:Stealth","Cosmetic:Caliburn:Alt","Cosmetic:Caliburn:Neon","Cosmetic:Caliburn:Clean","Cosmetic:Caliburn:Stealth","Cosmetic:BarracudaPrototype:Alt","Cosmetic:BarracudaPrototype:Neon","Cosmetic:BarracudaPrototype:Clean","Cosmetic:BarracudaPrototype:Stealth","Cosmetic:Orbitronix:Alt","Cosmetic:Orbitronix:Neon","Cosmetic:Orbitronix:Clean","Cosmetic:Orbitronix:Stealth","Cosmetic:Mako:Alt","Cosmetic:Mako:Neon","Cosmetic:Mako:Clean","Cosmetic:Mako:Stealth","Cosmetic:Protonic:Alt","Cosmetic:Protonic:Neon","Cosmetic:Protonic:Clean","Cosmetic:Protonic:Stealth","Cosmetic:Mtechp1:Alt","Cosmetic:Mtechp1:Neon","Cosmetic:Mtechp1:Clean","Cosmetic:Mtechp1:Stealth","Cosmetic:Nx2000:Alt","Cosmetic:Nx2000:Neon","Cosmetic:Nx2000:Clean","Cosmetic:Nx2000:Stealth","Cosmetic:Tempest:Alt","Cosmetic:Tempest:Neon","Cosmetic:Tempest:Clean","Cosmetic:Tempest:Stealth","Cosmetic:Zoneship:Neon","Cosmetic:Zoneship:Clean","Cosmetic:Zoneship:Stealth","Cosmetic:Gtek:Alt","Cosmetic:Gtek:Neon","Cosmetic:Gtek:Clean","Cosmetic:Gtek:Stealth","Ship:Gtek","Starter:Gtek","Starter:Wyvern","Starter:Hyperion","Starter:Scorpio","Starter:Omnicom","Starter:Diavolt","Starter:Nexus","Starter:Tenrai","Starter:Caliburn","Starter:Orbitronix","Starter:Mako","Starter:Tempest","Techno","Urban","Reflex","Ascension","Descension","Speed Freak","Zen Zeus","Finale A","Finale B","Finale C","Ship:Wyvern","Ship:Hyperion","Ship:Scorpio","Ship:Omnicom","Ship:Diavolt","Ship:Nexus","Ship:Tenrai","Ship:Caliburn","Ship:BarracudaPrototype","Ship:Orbitronix","Ship:Mako","Ship:Protonic","Ship:Mtechp1","Ship:Nx2000","Ship:Tempest","Ship:Zoneship","Zephyr Ridge Track Access","Harpstone Track Access","Aciknovae Track Access","Luna Track Access","Dover Track Access","Omega Track Access","Ishtar Track Access","Utah Project Track Access","Arrivon XI Track Access","Nova Split Track Access","Marina Rush Track Access","Apex Speed Access","Halberd Speed Access","Spectre Speed Access","Zen Speed Access"};
 public static readonly string[] Campaigns = new string[] {"Breeze","Techno","Urban","Reflex","Ascension","Descension","Speed Freak","Zen Zeus"};
 public static readonly EventSpec[] Events = new EventSpec[] {new EventSpec("bng05:21591000","Breeze","Cassandra Introduction Race",0,0,"Cassandra_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591001","Breeze","Zephyr Ridge Introduction Race",0,1,"ZephyrRidge_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591002","Breeze","Harpstone Introduction Race",0,2,"Harpstone_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591003","Breeze","Harpstone Pure Race",1,2,"Harpstone_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591004","Breeze","Breeze Tournament",3,2,"Cassandra_Normal|ZephyrRidge_Normal|Harpstone_Normal","Apex",false,"Race"),
new EventSpec("bng05:21591005","Techno","Aciknovae Introduction Race",0,0,"Aciknovae_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591006","Techno","Luna Introduction Race",0,1,"Luna_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591007","Techno","Dover Introduction Race",0,2,"Kbay_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591008","Techno","Luna Pure Race",2,1,"Luna_Normal","Apex",false,"Race"),
new EventSpec("bng05:21591009","Techno","Techno Tournament",3,2,"Aciknovae_Normal|Luna_Normal|Kbay_Normal","Apex",false,"Race"),
new EventSpec("bng05:21591010","Urban","Ishtar Citadel Introduction Race",0,0,"Ishtar_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591011","Urban","Omega Harbour Introduction Race",0,1,"Omega_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591012","Urban","Utah Project Introduction Race",0,2,"UtahProject_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591013","Urban","Utah Project Pure Race",2,2,"UtahProject_Normal","Apex",false,"Race"),
new EventSpec("bng05:21591014","Urban","Urban Tournament",3,2,"Ishtar_Normal|Omega_Normal|UtahProject_Normal","Halberd",false,"Race"),
new EventSpec("bng05:21591015","Reflex","Nova Split Introduction Race",0,0,"Nova Split","Toxic",false,"Race"),
new EventSpec("bng05:21591016","Reflex","Arrivon XI Introduction Race",0,1,"Arrivon XI","Toxic",false,"Race"),
new EventSpec("bng05:21591017","Reflex","Marina Rush Introduction Race",0,2,"MarinaRush_Normal","Toxic",false,"Race"),
new EventSpec("bng05:21591018","Reflex","Arrivon XI Pure Race",3,0,"Arrivon XI","Halberd",false,"Race"),
new EventSpec("bng05:21591019","Reflex","Reflex Tournament",3,2,"Nova Split|Arrivon XI|MarinaRush_Normal","Halberd",false,"Race"),
new EventSpec("bng05:21591020","Ascension","Cassandra Reverse Race",0,0,"Cassandra_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591021","Ascension","Harpstone Reverse Race",0,1,"Harpstone_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591022","Ascension","Luna Reverse Race",0,2,"Luna_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591023","Ascension","Harpstone Reverse Pure Race",1,1,"Harpstone_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591024","Ascension","Dover Reverse Race",2,0,"Kbay_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591025","Ascension","Utah Project Reverse Race",2,1,"UtahProject_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591026","Ascension","Ishtar Citadel Reverse Race",2,2,"Ishtar_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591027","Ascension","Hyperions Ascension 1v1",3,1,"UtahProject_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591028","Ascension","Ascension A Tournament",4,1,"Cassandra_Reverse|Harpstone_Reverse|Luna_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591029","Ascension","Ascension B Tournament",4,2,"Kbay_Reverse|UtahProject_Reverse|Ishtar_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591030","Descension","Nova Split Reverse Race",0,0,"Nova Split Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591031","Descension","Aciknovae Reverse Race",0,1,"Aciknovae_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591032","Descension","Omega Harbour Reverse Race",0,2,"Omega_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591033","Descension","Omega Harbour Pure Race",1,2,"Omega_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591034","Descension","Arrivon XI Reverse Race",2,0,"Arrivon XI Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591035","Descension","Zephyr Ridge Reverse Race",2,1,"ZephyrRidge_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591036","Descension","Marina Rush Reverse Race",2,2,"MarinaRush_Reverse","Apex",false,"Race"),
new EventSpec("bng05:21591037","Descension","Scorpios Descension 1v1",4,0,"ZephyrRidge_Reverse","Halberd",false,"Race"),
new EventSpec("bng05:21591038","Descension","Decension A Tournament",4,1,"Nova Split Reverse|Aciknovae_Reverse|Omega_Reverse","Spectre",false,"Race"),
new EventSpec("bng05:21591039","Descension","Decension B Tournament",4,2,"Arrivon XI Reverse|ZephyrRidge_Reverse|MarinaRush_Reverse","Spectre",false,"Race"),
new EventSpec("bng05:21591040","Speed Freak","Cassandra Race",0,0,"Cassandra_Normal","Spectre",false,"Race"),
new EventSpec("bng05:21591041","Speed Freak","Nexuses Speed Freak 1v1",1,0,"Cassandra_Normal","Spectre",false,"Race"),
new EventSpec("bng05:21591042","Speed Freak","Aciknovae Double Damage Race",2,0,"Aciknovae_Normal","Spectre",false,"Race"),
new EventSpec("bng05:21591043","Speed Freak","Luna Reverse Afterburner Challenge",2,1,"Luna_Reverse","Spectre",false,"Race"),
new EventSpec("bng05:21591044","Speed Freak","Speed Freak A Tournament",3,0,"Cassandra_Normal|ZephyrRidge_Reverse|Harpstone_Normal|Luna_Reverse","Spectre",false,"Race"),
new EventSpec("bng05:21591045","Speed Freak","Speed Freak B Tournament",3,1,"Nova Split|Aciknovae_Normal|Luna_Normal|UtahProject_Normal","Spectre",false,"Race"),
new EventSpec("bng05:21591046","Zen Zeus","Nova Split Afterburner Challenge",0,1,"Nova Split","Zen",false,"Race"),
new EventSpec("bng05:21591047","Zen Zeus","Cassandra Afterburner Challenge",1,1,"Cassandra_Normal","Zen",false,"Race"),
new EventSpec("bng05:21591048","Zen Zeus","Utah Project Endurance Race",2,0,"UtahProject_Normal","Zen",false,"Race"),
new EventSpec("bng05:21591049","Zen Zeus","Ishtar Citadel Double Damage Race",2,1,"Ishtar_Normal","Zen",false,"Race"),
new EventSpec("bng05:21591050","Zen Zeus","AGL 2159 Finale A",3,0,"Cassandra_Normal|ZephyrRidge_Normal|Harpstone_Normal|Aciknovae_Normal","Zen",false,"Race"),
new EventSpec("bng05:21591051","Zen Zeus","AGL 2159 Finale B",3,1,"Luna_Normal|Kbay_Normal|Ishtar_Normal|Omega_Normal","Zen",false,"Race"),
new EventSpec("bng05:21591052","Zen Zeus","AGL 2159 Finale C",3,2,"Nova Split|Arrivon XI|MarinaRush_Normal","Zen",false,"Race"),
new EventSpec("bng06:21591053","Breeze","G-Teks Cassandra Trial",1,0,"Cassandra_Normal","Toxic",false,"Time Trial"),
new EventSpec("bng06:21591054","Breeze","Scorpios Zephyr Ridge Trial",2,1,"ZephyrRidge_Normal","Toxic",false,"Time Trial"),
new EventSpec("bng06:21591055","Breeze","Hyperions Harpstone Trial",3,1,"Harpstone_Normal","Toxic",false,"Time Trial"),
new EventSpec("bng06:21591056","Techno","Omnicoms Luna Trial",1,1,"Luna_Normal","Apex",false,"Time Trial"),
new EventSpec("bng06:21591057","Techno","Diavolts Aciknovae Trial",2,0,"Aciknovae_Normal","Apex",false,"Time Trial"),
new EventSpec("bng06:21591058","Techno","Wyverns Dover Trial",3,1,"Kbay_Normal","Apex",false,"Time Trial"),
new EventSpec("bng06:21591059","Urban","Nexus Omega Trial",1,1,"Omega_Normal","Halberd",false,"Time Trial"),
new EventSpec("bng06:21591060","Urban","Tenrais Ishtar Trial",2,0,"Ishtar_Normal","Halberd",false,"Time Trial"),
new EventSpec("bng06:21591061","Urban","G-Teks Utah Trial",3,1,"UtahProject_Normal","Halberd",false,"Time Trial"),
new EventSpec("bng06:21591062","Reflex","Hyperions Arrivon XI Trial",1,1,"Arrivon XI","Halberd",false,"Time Trial"),
new EventSpec("bng06:21591063","Reflex","Omnicoms Nova Split Trial",2,1,"Nova Split","Halberd",false,"Time Trial"),
new EventSpec("bng07:21591064","Breeze","Zephyr Ridge Knockout",1,1,"ZephyrRidge_Normal","Toxic",false,"Knockout"),
new EventSpec("bng07:21591065","Breeze","Harpstone Knockout",2,2,"Harpstone_Normal","Toxic",false,"Knockout"),
new EventSpec("bng07:21591066","Techno","Luna Knockout",3,0,"Luna_Normal","Apex",false,"Knockout"),
new EventSpec("bng07:21591067","Urban","Utah Project Knockout",1,2,"UtahProject_Normal","Apex",false,"Knockout"),
new EventSpec("bng07:21591068","Reflex","Marina Rush Knockout",1,2,"MarinaRush_Normal","Halberd",false,"Knockout"),
new EventSpec("bng07:21591069","Descension","Arrivon XI Reverse Knockout",3,0,"Arrivon XI Reverse","Halberd",false,"Knockout"),
new EventSpec("bng07:21591070","Speed Freak","Nova Split Pure Knockout",0,2,"Nova Split","Spectre",false,"Knockout"),
new EventSpec("bng08:21591071","Breeze","Cassandra Survival",2,0,"Cassandra_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591072","Breeze","Zephyr Ridge Survival",3,0,"ZephyrRidge_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591073","Techno","Aciknovae Survival",1,0,"Aciknovae_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591074","Techno","Dover Survival",2,2,"Kbay_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591075","Urban","Ishtar Citadel Survival",1,0,"Ishtar_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591076","Urban","Omega Harbour Survival",2,1,"Omega_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591077","Reflex","Nova Split Survival",1,0,"Nova Split","Zen",false,"Survival"),
new EventSpec("bng08:21591078","Reflex","Arrivon XI Survival",2,0,"Arrivon XI","Zen",false,"Survival"),
new EventSpec("bng08:21591079","Reflex","Marina Rush Survival",3,1,"MarinaRush_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591080","Speed Freak","Harpstone Survival",0,1,"Harpstone_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591081","Speed Freak","Luna Survival",2,2,"Luna_Normal","Zen",false,"Survival"),
new EventSpec("bng08:21591082","Zen Zeus","Utah Project Survival",1,2,"UtahProject_Normal","Zen",false,"Survival"),
new EventSpec("bng09:21591083","Techno","Dover Rush Hour",1,2,"Kbay_Normal","Apex",false,"Rush Hour"),
new EventSpec("bng09:21591084","Ascension","Cassandra Reverse Rush Hour",4,0,"Cassandra_Reverse","Halberd",false,"Rush Hour"),
new EventSpec("bng09:21591085","Descension","Zephyr Ridge Reverse Rush Hour",3,1,"ZephyrRidge_Reverse","Halberd",false,"Rush Hour"),
new EventSpec("bng09:21591086","Zen Zeus","Marina Rush Hour",0,2,"MarinaRush_Normal","Zen",false,"Rush Hour"),
new EventSpec("bng10:21591087","Ascension","Cassandra Reverse Eliminator",1,0,"Cassandra_Reverse","Toxic",false,"Eliminator"),
new EventSpec("bng10:21591088","Ascension","Ishtar Citadel Reverse Eliminator",3,2,"Ishtar_Reverse","Toxic",false,"Eliminator"),
new EventSpec("bng10:21591089","Descension","Aciknovae Reverse Eliminator",1,1,"Aciknovae_Reverse","Toxic",false,"Eliminator"),
new EventSpec("bng10:21591090","Speed Freak","Utah Project Eliminator",1,2,"UtahProject_Normal","Toxic",false,"Eliminator"),
new EventSpec("bng10:21591091","Zen Zeus","Dover Eliminator",0,0,"Kbay_Normal","Toxic",false,"Eliminator"),
new EventSpec("bng11:21591092","Urban","Ishtar Citadel Upsurge",3,0,"Ishtar_Normal","Zen",false,"Upsurge"),
new EventSpec("bng11:21591093","Reflex","Marina Rush Team Race",2,2,"MarinaRush_Normal","Toxic",false,"Team Race"),
new EventSpec("bng11:21591094","Ascension","Luna Reverse Team Race",1,2,"Luna_Reverse","Apex",false,"Team Race"),
new EventSpec("bng11:21591095","Ascension","Dover Reverse Upsurge",3,0,"Kbay_Reverse","Zen",false,"Upsurge"),
new EventSpec("bng11:21591096","Descension","Nova Split Reverse Team Race",1,0,"Nova Split Reverse","Halberd",false,"Team Race"),
new EventSpec("bng11:21591097","Descension","Marina Rush Reverse Upsurge",3,2,"MarinaRush_Reverse","Zen",false,"Upsurge"),
new EventSpec("bng11:21591098","Speed Freak","Zephyr Ridge Reverse Team Race",1,1,"ZephyrRidge_Reverse","Apex",false,"Team Race"),
new EventSpec("bng11:21591099","Zen Zeus","Omega Harbour Team Race",1,0,"Omega_Normal","Apex",false,"Team Race"),
new EventSpec("bng11:21591100","Zen Zeus","Nova Split Upsurge",2,2,"Nova Split","Zen",false,"Upsurge")};
}}
