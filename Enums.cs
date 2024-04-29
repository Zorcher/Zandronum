using System;

namespace Zandronum
{
	[Flags]
	internal enum ServerQueryFlags
	{
		Name = 0x00000001,
		Url = 0x00000002,
		Email = 0x00000004,
		MapName = 0x00000008,
		MaxClients = 0x00000010,
		MaxPlayers = 0x00000020,
		PWads = 0x00000040,
		GameType = 0x00000080,
		GameName = 0x00000100,
		IWad = 0x00000200,
		ForcePassword = 0x00000400,
		ForceJoinPassword = 0x00000800,
		GameSkill = 0x00001000,
		BotSkill = 0x00002000,
		DMFlags = 0x00004000,
		Limits = 0x00010000,
		TeamDamage = 0x00020000,
		TeamScores = 0x00040000, // Deprecated
		NumPlayers = 0x00080000,
		PlayerData = 0x00100000,
		TeamInfoNumber = 0x00200000,
		TeamInfoName = 0x00400000,
		TeamInfoColor = 0x00800000,
		TeamInfoScore = 0x01000000,
		TestingServer = 0x02000000,
		DataMD5Sum = 0x04000000
	}

	[Flags]
	internal enum DMFlags1
	{
		NoHealth = 0x00000001,
		NoItems = 0x00000002,
		WeaponStay = 0x00000004,
		OldFallDamage = 0x00000008,
		HexenFallDamage = 0x00000010,
		SameLevel = 0x00000040,
		SpawnFarthest = 0x00000080,
		ForceRespawn = 0x00000100,
		NoArmor = 0x00000200,
		NoExit = 0x00000400,
		InfiniteAmmo = 0x00000800,
		NoMonsters = 0x00001000,
		MonsterRespawn = 0x00002000,
		ItemRespawn = 0x00004000,
		FastMonsters = 0x00008000,
		NoJumping = 0x00010000,
		NoFreeLook = 0x00020000,
		RespawnSuper = 0x00040000,
		NoFOV = 0x00080000,
		NoWeaponSpawn = 0x00100000,
		NoCrouching = 0x00200000,
		CoopLoseInventory = 0x00400000,
		CoopLoseKeys = 0x00800000,
		CoopLoseWeapons = 0x01000000,
		CoopLoseArmor = 0x02000000,
		CoopLosePowerups = 0x04000000,
		CoopLoseAmmo = 0x08000000,
		CoopHalfAmmo = 0x10000000
	}

	[Flags]
	internal enum DMFlags2
	{
		WeaponDrop = 0x00000002,
		NoRunes = 0x00000004,
		InstantReturn = 0x00000008,
		NoTeamSwitch = 0x00000010,
		NoTeamSelect = 0x00000020,
		DoubleAmmo = 0x00000040,
		Degeneration = 0x00000080,
		BfgFreeAim = 0x00000100,
		BarrelRespawn = 0x00000200,
		NoRespawnInvulnerability = 0x00000400,
		ShotgunStart = 0x00000800,
		SameSpawnSpot = 0x00001000,
		SameTeam = 0x00002000,
		ForceGlDefaults = 0x00040000,
		AwardDamageInsteadOfKills = 0x00100000,
		CoopSpActorSpawn = 0x20000000
	}

	[Flags]
	internal enum CompatFlags1
	{
		ShortTextures = 0x00000001,
		Stairs = 0x00000002,
		LimitPain = 0x00000004,
		SilentPickup = 0x00000008,
		NoPassOver = 0x00000010,
		SoundSlots = 0x00000020,
		WallRun = 0x00000040,
		NoTossDrops = 0x00000080,
		UseBlocking = 0x00000100,
		NoDoorLight = 0x00000200,
		RavenScroll = 0x00000400,
		SectorSound = 0x00000800,
		DehHealth = 0x00001000,
		Trace = 0x00002000,
		Dropoff = 0x00004000,
		BoomScroll = 0x00008000,
		Invisiblity = 0x00010000,
		LimitedAirMovement = 0x00020000,
		PlasmaBumpBug = 0x00040000,
		InstantRespawn = 0x00080000,
		DisableTaunts = 0x00100000,
		OriginalSoundCurve = 0x00200000,
		OldIntermission = 0x00400000,
		DisableStealthMonsters = 0x00800000,
		OldRadiusDMG = 0x01000000,
		NoCrossHair = 0x02000000,
		OldWeaponSwitch = 0x04000000
	}

	[Flags]
	internal enum CompatFlags2
	{
		NetScriptsClientSide = 0x00000001,
		ClientsSendFullButtonInfo = 0x00000002,
		NoLand = 0x00000004
	}

	[Flags]
	internal enum LMSFlags
	{
		Pistol = 0x00000001,
		Shotgun = 0x00000002,
		SuperShotgun = 0x00000004,
		Chaingun = 0x00000008,
		Minigun = 0x00000010,
		RocketLauncher = 0x00000020,
		GrenadeLauncher = 0x00000040,
		Plasma = 0x00000080,
		Railgun = 0x00000100,
		Chainsaw = 0x00000200
	}
}
