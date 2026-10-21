# Version 5.0.0 for SPT ~4.1.6

The biggest update in the history of this mod. I sit down and just tried to resolve every little issue I have collected, add every feature that I wanted and make changes I dreamed of adding for a long time.
This includes: new trader, web config, localization system, client mod and APBS blacklist rotation.

New trader is resolving the issue where all the items from this mod were scattered between all other traders.
Web config is resolving the config issue, where json config was easily being broken by simply loosing one ",".
Localization system allows for better item naming scheme and adding new languages for this mod.
Client mod is resolving the issues with a new slot added for Variant Cores as well as adding an distinction cue for Variant Weapons.
APBS blacklist rotation allows APBS users to sometimes see other modded weapons, despite having Definitive Weapon Variants installed.

## New Feature: Variant Trader - Merchant
- "An Italian mafioso who started out smuggling unusual weapons across borders but ended up collecting them"
- You no longer need to check all the traders for an item that fits your Variant Weapon
- Merchant trader is available from the start
- Static/Dynamic assort\
    Almost all items added by this mod are available at any time\
    15 random variant weapons are available each trader refresh
- Loyalty Levels are only tied to the character level:\
    LL2 => 15 character level\
	LL3 => 30 character level\
	LL4 => 45 character level
- Moved all items to Merchant
- Moved 12.7x108 barter to new trader
- Removed AmonyaTraderMode
- Removed TraderId from all config options

## New Feature: Blazor config
- Added blazor web page for config changes\
    Available in /definitive-weapon-variants/config or from launcher
- defaultConfig.jsonc is no longer used; config.jsonc is created from built-in defaults when missing
- You need to config the mod after this update using Blazor page
- All changes done to config page require a normal server restart

## New Feature: Localization system
- Added localization system, now DWV can be easely translatable\
    If you can manage 800+ lines of text, it is!
- All items has their names and description checked and standardized

## New Feature: Client mod
- All Variant Weapons now have special effects:\
    On firing\
    ToDo: On magazine reload\
    ToDo: Idle animation
- Variant Cores are easier to remove and put back into slot, even when operating on equiped weapon
- Errors from Variant Cores special mod_slot slot are now properly silenced
- mod_slot is now a "required" slot by default with it's functionality still available

## New Feature: APBS blacklist rotation
- Added automatic swapping of APBS blacklist based on time
- Allows for less variants in the overall pool of the APBS modded weapons, at the same time, providing the same randomness
- System is replacing a set of Weapon Variants every hour\
    Both can be changed in config
- Blacklist refresh is also automatic and does not require restarting the server\
    You don't need a restart on top of restarting the server!

## Major change: Universal Cores system refresh
- Universal Cores now have themes instead of generic number
- Renamed 9 and added 11 new Universal Cores across 2 themes: Order and Chaos
- Order theme Universal Cores are providing increased Ergonomics and are decreasing recoil
- Chaos theme Universal Cores are providing a lot of one stat with the price of the other
- Universal Cores drop rate from Unknown Packages are now tied 1:1 to Unique Cores drop rate\
    Can be increased in the config
- You can add your own themes and Universal Cores properties in db/06_Cores folder\
    Don't forget about locales or you will be missing names for them!

### Content Update
- Added 4 new Unique Weapon Variants:\
    Glock 18C >Glock 90P<\
    RPDN >Cute<\
    HK 416A5 >One Raid Only<\
    9A-91 >Balanced<
- Added new Niche Variant Weapon Type -  Universal Adapter
- Added new Niche Variant Weapon Type -  Tiny
- Added new Baseline Variant Weapon Type -  Stendo\
    Pistol variant allowing the use of wider variety of magazines from its caliber, as well as 3 extended magazines: 40-round, 50-round or even 70-round\
    15 weapons and 3 new magazines
- Added new Meme Variant Weapon Type -  Double Barrel
- Added 21 new items\
    Blicky Special Magical Unbelievable Magazine v2 for Blicky weapon variants\
    => Available from Merchant LL4 for 1 Unique, Ultimate and Superior Variant Cores\
    Anti-materiel rifle 12.7x108mm 2-round extended internal magazine\
    => Available from Merchant LL3 for 1 Ultimate Variant Core\
    50Cal .50 BMG 10-round extended internal magazine\
    => Available from Merchant LL3 for 1 Ultimate Variant Core\
    Karabin Spetsialniy 23x75mm 18-shell extended magazine cap\
    => Available from Merchant LL2 for 1 Superior Variant Core\
    Black Market 5.56x45mm/.300 45-round ergonomic magazine\
    => Available from Merchant LL2 for 1 Superior Variant Core\
    Black Market 5.56x45mm/.300 55-round ergonomic magazine\
    => Available from Merchant LL3 for 59000 roubles and 1 Superior Variant Core\
    Assault Rifle 5.56x45mm 20-round pistol-sized magazinee\
    => Available from Merchant LL1 for 1 Advanced Variant Core\
    Assault Rifle 5.56x45mm 30-round pistol-sized magazine\
    => Available from Merchant LL3 for 18000 roubles and 1 Advanced Variant Core\
    Light Machine Gun 7.62x54R 50-round magazine\
    => Available from Merchant LL1 for 25000 roubles\
    Light Machine Gun 7.62x54R 77-round optimized magazine\
    => Available from Merchant LL4 for 25000 roubles and 1 Advanced Variant Core\
    Sniper 660mm barrel\
    => Available from Merchant LL2 for 23000 roubles\
    Shotgun 12-gauge 15-round half-drum magazine\
    => Available from Merchant LL2 for 1 Advanced Variant Core\
    FURY .277 19-round magazine\
    => Available from Merchant LL1 for 17000 roubles\
    World War II 7.62x25 55-round magazine\
    => Available from Merchant LL4 for 1 Baseline Variant Core\
    World War II 7.62x25 90-round extended magazine\
    => Available from Merchant LL2 for 1 Baseline Variant Core\
    Slav "Thing" 7.62x39 25-round ultimate magazine\
    => Available from Merchant LL2 for 1 Baseline Variant Core\
    Freedom "Thing" 5.56x45 99-round ultimate magazine\
    => Available from Merchant LL2 for 1 Baseline Variant Core\
    Smoothbore 7.62x39/.366TKM 21-round small extended magazine\
    => Available from Merchant LL1 for 1 Baseline Variant Core\
    Quick-scope ELCAN Specter HCO holographic sight\
    => Available from Merchant LL1 for 1 Flawed Variant Core\
    Quick-scope BelOMO PK-AA dovetail reflex sight\
    => Available from Merchant LL1 for 1 Flawed Variant Core\
    Early Prototype 7.62x54r 30-round magazine\
    => Available from Merchant LL3 for 5 Meme Variant Cores\
    Desert .50AE 84-round drum magazine\
    => Available from Merchant LL2 for 5 Meme Variant Cores
- Added Blicky to the Lapua Variant Type
    SMUM magazines can now hold .338 Lapua Magnum caliber
- Added items from Backport AddOn to the main mod
- Added QBZ-191 to Heavy Machine Gun, Light Machine Gun and Marksman Variant Types

### Balance Patch
- Moved Lapua .338 20-round magazine to Merchant LL2
- Moved Lapua .338 30-round magazine to Merchant LL3
- PPSh-41 >Actually Endless< 7.62x25 4999-round drum magazine\
    Weight increased: -21 => -10
- \>Stormtrooper< Nightforce ATACR 7-35x56 34mm riflescope\
    Weight increased: -20 => -10

### Other changes
- Changed minimum SPT requirements to 4.1.6
- User-added changes to Variant Types are now supported\
    Added "ReplaceExisting" key to use when you want to change something in Variant Type, it tells the loader to load your copy instead of original\
    Just copy an existing variant or item, add "ReplaceExisting": true and you are good to go!
- Added a mod check to variant types and weapons\
    This should be used only for modded items shipped with this mod

### Bugfixes
- Fixed disabling generation of any variant quality causing mod to stop working
- Fixed missing Close Quarters description
- Variant Cores no longer has green icon indicating a stock item

# Version 4.3.2 for SPT ~4.1.2

- Initial release for SPT 4.1.2

----------
# Version 4.3.1 for SPT ~4.0.13

### Bugfixes
- Fixed incorrect trades for upgrading cores
- Fixed missing trades for new cases
----------

# Version 4.3.0 for SPT ~4.0.13

## Variant Cores System Update and Balance Patch
New storage cases for Variant Cores, more config options, many variant types changing quality, 36 new weapons (including 6 uniques)!

### Variant Cores System Update
- Added Expeditionary Variant Core Case that allows storing up to 20 of every type of normal Variant Core and can be placed inside secure containers. Can be purchased with 12 Baseline Cores
- Added Variant Core Carry Case that can store a large number of Variant Cores in your inventory at once. It has 40 slots for normal and Locked Cores, 10 slots for Universal Cores and 50 slots dedicated for Unknown Packages and Blind Boxes. Can be purchased with 30 Baseline Cores
- Added a 3:1 exchange option for upgrading Variant Cores. By default, upgrading is only available up to Baseline quality
- Adjusted the probabilities of items received from Unknown Packages and added Locked Cores to the loot pool
- Locked Cores can be broken into normal ones in cultist circle
- Added 4 new Universal Variant Cores
- Unknown Package weight has been lowered and can now be adjusted in the config
- Added many other options to the config file\
    If you want to make any changes to the Variant Core system, update your config. Instructions can be found in the mod description

### Content Update
- Added 6 new Unique Variant Weapons:\
AA-12 Gen 2 >Endless Launcher<\
RFB >Automatic No-Scope<\
VPO-215 >Recoiless Lapua<\
AK-103 >Russian Market<\
MP5K-N >Handy Assault Rifle<\
Blicky >Neverending<
- New Baseline Variant - Smoothbore\
Weapon variant capable of using both .366 TKM and 7.62x39mm ammunition (primary caliber: .366 TKM)\
Containing 9 new weapons
- New Niche Variant - Railgun\
Weapon variant chambered in 40x46mm grenades with unbelievable muzzle velocity, allowing grenades to be launched at nearly normal bullet speeds\
Containing 4 new weapons
- New Meme Variant - Early Prototype\
Weapon variant chambered in 7.62x54mmR that can use a larger variety of magazines\
Containing 11 new weapons


### Balance Patch
- Lapua\
Quality lowered from Ultimate to Superior\
Added exclusive 20-round and 30-round magazines for this variant
- Assault Rifle\
Quality lowered from Superior to Advanced
- Overclocked\
Quality lowered from Advanced to Baseline
- No-Scope\
Quality lowered from Flawed to Meme\
Increased ergonomics from +15 to +25\
Now has increased fire rate\
Added RFB to this variant type
- Close Quarters\
Now has reduced recoil, fire rate and muzzle velocity\
Added exclusive 30-round and 40-round magazines for this variant\
Changed description
- Endless\
Added a new 40-round magazine for bolt-action rifles\
Added AXMC, SV-98, Mosin Sniper, M700 and TRG M10 to this variant type
- FURY\
Quality increased from Baseline to Advanced
- Slav "Thing"\
Quality increased from Meme to Baseline
- Freedom "Thing"\
Quality increased from Meme to Baseline
- Converted\
Quality increased from Meme to Flawed
- Glass Cannon\
Quality lowered from Niche to Meme
- BFG-50\
Quality increased from Meme to Flawed
- SKS Unique\
Fire rate increased from 40 to 469

### Other
- Moved required SPT version to 4.0.13
- Fixed background colors of items when modified by other mods
- ----------