﻿using System;
using System.Collections.Generic;
using UnityEngine;


namespace KERBALISM
{


	public sealed class Vessel_info
	{
		public Vessel_info(Vessel v, uint vessel_id, UInt64 inc)
		{
			// NOTE: anything used here can't in turn use cache, unless you know what you are doing

			// NOTE: you can't cache vessel position
			// at any point in time all vessel/body positions are relative to a different frame of reference
			// so comparing the current position of a vessel, with the cached one of another make no sense

			// associate with an unique incremental id
			this.inc = inc;

			// determine if this is a valid vessel
			is_vessel = Lib.IsVessel(v);
			if (!is_vessel)
				return;

			// determine if this is a rescue mission vessel
			is_rescue = Misc.IsRescueMission(v);
			if (is_rescue)
				return;

			// dead EVA are not valid vessels
			if (EVA.IsDead(v))
				return;

			// shortcut for common tests
			is_valid = true;

			// generate id once
			id = vessel_id;

			// calculate crew info for the vessel
			crew_count = Lib.CrewCount(v);
			crew_capacity = Lib.CrewCapacity(v);

			// get vessel position
			Vector3d position = Lib.VesselPosition(v);

			// this should never happen again
			if (Vector3d.Distance(position, v.mainBody.position) < 1.0)
			{
				throw new Exception("Shit hit the fan for vessel " + v.vesselName);
			}

			// determine if there is enough EC for a powered state
			powered = ResourceCache.Info(v, "ElectricCharge").amount > double.Epsilon;

			// determine if in sunlight, calculate sun direction and distance
			sunlight = Sim.RaytraceBody(v, position, FlightGlobals.Bodies[0], out sun_dir, out sun_dist) ? 1.0 : 0.0;

			// environment stuff
			atmo_factor = Sim.AtmosphereFactor(v.mainBody, position, sun_dir);
			gamma_transparency = Sim.GammaTransparency(v.mainBody, v.altitude);
			underwater = Sim.Underwater(v);
			breathable = Sim.Breathable(v, underwater);
			landed = Lib.Landed(v);

			if (v.mainBody.flightGlobalsIndex != 0 && TimeWarp.CurrentRate > 1000.0f)
			{
				highspeedWarp(v);
			}

			// temperature at vessel position
			temperature = Sim.Temperature(v, position, sunlight, atmo_factor, out solar_flux, out albedo_flux, out body_flux, out total_flux);
			temp_diff = Sim.TempDiff(temperature, v.mainBody, landed);

			// radiation
			radiation = Radiation.Compute(v, position, gamma_transparency, sunlight, out blackout, out magnetosphere, out inner_belt, out outer_belt, out interstellar);

			// extended atmosphere
			thermosphere = Sim.InsideThermosphere(v);
			exosphere = Sim.InsideExosphere(v);

			// malfunction stuff
			malfunction = Reliability.HasMalfunction(v);
			critical = Reliability.HasCriticalFailure(v);

			// communications info
			connection = new ConnectionInfo(v, powered, blackout);
			transmitting = Science.Transmitting(v, connection.linked && connection.rate > double.Epsilon);

			// habitat data
			volume = Habitat.Tot_volume(v);
			surface = Habitat.Tot_surface(v);
			pressure = Habitat.Pressure(v);
			poisoning = Habitat.Poisoning(v);
			humidity = Habitat.Humidity(v);
			shielding = Habitat.Shielding(v);
			living_space = Habitat.Living_space(v);
			comforts = new Comforts(v, landed, crew_count > 1, connection.linked && connection.rate > double.Epsilon);

			// data about greenhouses
			greenhouses = Greenhouse.Greenhouses(v);

			// other stuff
			gravioli = Sim.Graviolis(v);
		}

		// at the two highest timewarp speed, the number of sun visibility samples drop to the point that
		// the quantization error first became noticeable, and then exceed 100%
		// to solve this, we switch to an analytical estimation of the portion of orbit that was in sunlight
		// - we check against timewarp rate, instead of index, to avoid issues during timewarp blending
		public void highspeedWarp(Vessel v)
		{
			// don't re-calculate this on every tick. So, if sunlight is not 1.0 or 0.0, do nothing here
			if (sunlight > 0.0001 && sunlight < 0.9999)
			{ 
				return;
			}

			sunlight = 1.0 - Sim.ShadowPeriod(v) / Sim.OrbitalPeriod(v);
			solar_flux = Sim.SolarFlux(Sim.SunDistance(Lib.VesselPosition(v))) * atmo_factor;
		}

		public UInt64 inc;                  // unique incremental id for the entry
		public bool is_vessel;              // true if this is a valid vessel
		public bool is_rescue;              // true if this is a rescue mission vessel
		public bool is_valid;               // equivalent to (is_vessel && !is_rescue && !eva_dead)
		public UInt32 id;                   // generate the id once
		public int crew_count;              // number of crew on the vessel
		public int crew_capacity;           // crew capacity of the vessel
		public double sunlight;             // if the vessel is in direct sunlight
		public Vector3d sun_dir;            // normalized vector from vessel to sun
		public double sun_dist;             // distance from vessel to sun
		public double solar_flux;           // solar flux at vessel position
		public double albedo_flux;          // solar flux reflected from the nearest body
		public double body_flux;            // infrared radiative flux from the nearest body
		public double total_flux;           // total flux at vessel position
		public double temperature;          // vessel temperature
		public double temp_diff;            // difference between external and survival temperature
		public double radiation;            // environment radiation at vessel position
		public bool magnetosphere;          // true if vessel is inside a magnetopause (except the heliosphere)
		public bool inner_belt;             // true if vessel is inside a radiation belt
		public bool outer_belt;             // true if vessel is inside a radiation belt
		public bool interstellar;           // true if vessel is outside sun magnetopause
		public bool blackout;               // true if the vessel is inside a magnetopause (except the sun) and under storm
		public bool thermosphere;           // true if vessel is inside thermosphere
		public bool exosphere;              // true if vessel is inside exosphere
		public double atmo_factor;          // proportion of flux not blocked by atmosphere
		public double gamma_transparency;   // proportion of ionizing radiation not blocked by atmosphere
		public bool underwater;             // true if inside ocean
		public bool breathable;             // true if inside breathable atmosphere
		public bool landed;                 // true if on the surface of a body
		public bool malfunction;            // true if at least a component has malfunctioned or had a critical failure
		public bool critical;               // true if at least a component had a critical failure
		public ConnectionInfo connection;   // connection info
		public string transmitting;         // name of file being transmitted, or empty
		public double volume;               // enabled volume in m^3
		public double surface;              // enabled surface in m^2
		public double pressure;             // normalized pressure
		public double poisoning;            // waste atmosphere amount versus total atmosphere amount
		public double humidity;             // moist atmosphere amount
		public double shielding;            // shielding level
		public double living_space;         // living space factor
		public Comforts comforts;           // comfort info
		public List<Greenhouse.Data> greenhouses; // some data about greenhouses
		public double gravioli;             // gravitation gauge particles detected (joke)
		public bool powered;                // true if vessel is powered
	}


	public static class Cache
	{
		public static void Init()
		{
			vessels = new Dictionary<uint, Vessel_info>();
			next_inc = 0;
		}


		public static void Clear()
		{
			vessels.Clear();
			next_inc = 0;
		}


		public static void Purge(Vessel v)
		{
			vessels.Remove(Lib.VesselID(v));
		}


		public static void Purge(ProtoVessel pv)
		{
			vessels.Remove(Lib.VesselID(pv));
		}


		public static void Update()
		{
			// purge the oldest entry from the vessel cache
			if (vessels.Count > 0)
			{
				UInt64 oldest_inc = UInt64.MaxValue;
				UInt32 oldest_id = 0;
				foreach (KeyValuePair<UInt32, Vessel_info> pair in vessels)
				{
					if (pair.Value.inc < oldest_inc)
					{
						oldest_inc = pair.Value.inc;
						oldest_id = pair.Key;
					}
				}
				vessels.Remove(oldest_id);
			}
		}


		public static Vessel_info VesselInfo(Vessel v)
		{
			// get vessel id
			UInt32 id = Lib.VesselID(v);

			// get the info from the cache, if it exist
			Vessel_info info;
			if (vessels.TryGetValue(id, out info))
				return info;

			// compute vessel info
			info = new Vessel_info(v, id, next_inc++);

			// store vessel info in the cache
			vessels.Add(id, info);

			// return the vessel info
			return info;
		}


		public static bool HasVesselInfo(Vessel v, out Vessel_info vi)
		{
			return vessels.TryGetValue(Lib.VesselID(v), out vi);
		}


		// vessel cache
		static Dictionary<UInt32, Vessel_info> vessels;

		// used to generate unique id
		static UInt64 next_inc;
	}


} // KERBALISM