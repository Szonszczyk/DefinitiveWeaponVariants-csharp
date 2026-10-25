# Version 5.0.0-beta.1

- Initial deploy of v5.0.0

# Version 5.0.0 for SPT ~4.1.6

The biggest update in the history of this mod. I sat down and tried to resolve every little issue I had collected, add every feature I wanted and make the changes I had dreamed of for a long time
This includes a new trader, a web config, a localization system, a client mod and APBS blacklist rotation

The new trader brings together all the items from this mod that were previously scattered across other traders
The web config makes configuration easier, avoiding JSON syntax errors caused by something as simple as a missing comma
The localization system provides a better item naming scheme and makes it easier to add new languages to the mod
The client mod fixes issues with the new Variant Core slot and adds visual cues to distinguish Variant Weapons and Variant Core items
APBS blacklist rotation allows APBS users to encounter other modded weapons more often while using Definitive Weapon Variants

## New Feature: Variant Trader - Merchant
- "An Italian mafioso who started out smuggling unusual weapons across borders but ended up collecting them"
- You no longer need to check all the traders for an item that fits your Variant Weapon
- Merchant is available from the start
- Static and dynamic assortments\
    Almost all items added by this mod are available at any time\
    15 random variant weapons are available after each trader refresh
- Loyalty Levels depend only on your character level:\
    LL2 => character level 15\
	LL3 => character level 30\
	LL4 => character level 45
- Moved all items to Merchant
- Moved the 12.7x108 barter to Merchant
- Removed the AmonyaTraderMode config option
- Removed TraderId from all config options

## New Feature: Blazor config
- Added a Blazor web page for configuring the mod\
    Available at /definitive-weapon-variants/config or through the launcher
- defaultConfig.jsonc is no longer used; config.jsonc is created from built-in defaults when missing
- You need to configure the mod using the Blazor page after this update
- All changes made on the config page require a server restart

## New Feature: Localization system
- Added a localization system, making DWV easier to translate\
    As long as you can manage 800+ lines of text!
- Reviewed and standardized all item names and descriptions

## New Feature: Client mod
- All Variant Weapons now have special effects:\
    Firing effects\
    Idle animations
- Variant Cores are easier to remove and put back into their slots, even on an equipped weapon
- Errors from the special mod_slot slot for Variant Cores are now properly silenced
- The mod_core slot is now a "required" slot by default while retaining its functionality
- The mod_core slot image add-on is now included in the main mod

## New Feature: APBS blacklist rotation
- Added automatic, timed rotation of the APBS blacklist
- Reduces the number of variants in the APBS modded weapon pool while maintaining the same randomness
- The system changes which Weapon Variants are blacklisted every hour\
    Both the set size and the rotation interval can be changed in the config
- The blacklist also refreshes automatically without requiring a server restart\
    Updates take effect while the server is running!

## Major change: Universal Cores system refresh
- Universal Cores now have themes instead of generic numbers
- Renamed 9 and added 11 new Universal Cores across 2 themes: Order and Chaos
- Order-themed Universal Cores increase ergonomics and reduce recoil
- Chaos-themed Universal Cores greatly increase one stat at the expense of another
- The drop rate for Universal Cores from Unknown Packages now matches the drop rate for Unique Cores\
    Can be increased in the config
- You can add your own themes and Universal Core properties in the db/06_Cores folder\
    Don't forget to add localization entries, or your cores will have no names!

### Content Update
- Added 4 new Unique Weapon Variants:\
    Glock 18C >Glock 90P<\
    RPDN >Cute<\
    HK 416A5 >One Raid Only<\
    9A-91 >Balanced<
- Added a new Niche Variant Weapon Type - Universal Adapter\
    A weapon variant whose kills count as kills with any weapon. To make it even more universal, its pistol grip, muzzle and stock slots also support a wider selection of attachments\
    6 weapons
- Added a new Niche Variant Weapon Type - Tiny\
    A Weapon Variant Type that takes up very little space in your stash. So little, in fact, that it can end up taking up no space at all!\
    7 weapons
- Added a new Baseline Variant Weapon Type - Stendo\
    A pistol variant that supports a wider variety of magazines in its caliber, as well as three extended magazines: 40-round, 50-round or even 70-round\
    15 weapons and 3 new magazines
- Added a new Meme Variant Weapon Type - Double Barrel\
    A weapon variant with a special barrel that allows another barrel to be mounted on top, with all the advantages and disadvantages that entails\
    10 weapons
- Added 21 new items\
    Blicky Special Magical Unbelievable Magazine v2 for Blicky weapon variants\
    => Available from Merchant LL4 for 1 Unique, 1 Ultimate and 1 Superior Variant Core\
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
    Assault Rifle 5.56x45mm 20-round pistol-sized magazine\
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
- Added the Blicky to the Lapua Variant Type\
    SMUM magazines can now hold .338 Lapua Magnum ammunition
- Added items from the Backport AddOn to the main mod
- Added the QBZ-191 to the Heavy Machine Gun, Light Machine Gun and Marksman Variant Types

### Balance Patch
- Moved the Lapua .338 20-round magazine to Merchant LL2
- Moved the Lapua .338 30-round magazine to Merchant LL3
- PPSh-41 >Actually Endless< 7.62x25 4999-round drum magazine\
    Weight increased: -21 => -10
- \>Stormtrooper< Nightforce ATACR 7-35x56 34mm riflescope\
    Weight increased: -20 => -10

### Other changes
- Raised the minimum supported SPT version to 4.1.6
- User-added changes to Variant Types are now supported\
    Added the "ReplaceExisting" key for modifying a Variant Type; it tells the loader to use your copy instead of the original\
    Just copy an existing variant or item, add "ReplaceExisting": true and you are good to go!
- Added a mod check to variant types and weapons\
    This should be used only for modded items shipped with this mod
- Added the BFG-50 to the APBS exclusion list
- The Variant Core slot is now required by default
- Variants no longer appear at Fence or on the Flea Market by default
- The option to disable Variant Cores now only removes the Variant Core slot

### Bugfixes
- Fixed an issue where disabling generation for any variant quality caused the mod to stop working
- Fixed the M3 Super 90 Karabin Spetsialniy getting stuck when reloading with an empty chamber
- Fixed the missing Close Quarters description
- Variant Cores no longer have the green icon indicating a stock item
- SMUM magazines (for Blicky variants) can now accept 20x1mm ammunition; yes, this was a bug, and yes, the special magazine for the Blicky did not accept Blicky ammunition

----------
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