﻿// ValheimRAFT, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// ValheimRAFT.MoveableBaseShipComponent

using System;
using System.Linq;
using UnityEngine;
using ValheimRAFT;
using ValheimRAFT.Util;

public class MoveableBaseShipComponent : MonoBehaviour
{
  [Flags]
  public enum MBFlags
  {
    None = 0,
    IsAnchored = 1,
    HideMesh = 2
  }

  internal MoveableBaseRootComponent m_baseRoot;

  internal Rigidbody m_rigidbody;

  internal Ship m_ship;

  internal ZNetView m_nview;

  internal GameObject m_baseRootObject;

  internal ZSyncTransform m_zsync;

  public float m_targetHeight;

  public float m_balanceForce = 0.03f;

  public float m_liftForce = 20f;

  public MBFlags m_flags;

  public void Awake()
  {
    Ship ship = GetComponent<Ship>();
    m_nview = GetComponent<ZNetView>();
    m_zsync = GetComponent<ZSyncTransform>();
    m_ship = GetComponent<Ship>();
    m_baseRootObject = new GameObject
    {
      name = "MoveableBase",
      layer = 0
    };
    m_baseRoot = m_baseRootObject.AddComponent<MoveableBaseRootComponent>();
    m_nview.Register("SetAnchor",
      delegate(long sender, bool state) { RPC_SetAnchor(sender, state); });
    m_nview.Register("SetVisual",
      delegate(long sender, bool state) { RPC_SetVisual(sender, state); });
    m_baseRoot.m_moveableBaseShip = this;
    m_baseRoot.m_nview = m_nview;
    m_baseRoot.m_ship = ship;
    m_baseRoot.m_id = ZDOPersistantID.Instance.GetOrCreatePersistantID(m_nview.m_zdo);
    m_rigidbody = GetComponent<Rigidbody>();
    m_baseRoot.m_syncRigidbody = m_rigidbody;
    m_rigidbody.mass = 1000f;
    m_baseRootObject.transform.SetParent(null);
    m_baseRootObject.transform.position = base.transform.position;
    m_baseRootObject.transform.rotation = base.transform.rotation;
    ship.transform.Find("ship/visual/mast")?.gameObject.SetActive(value: false);
    ship.transform.Find("ship/colliders/log")?.gameObject.SetActive(value: false);
    ship.transform.Find("ship/colliders/log (1)")?.gameObject.SetActive(value: false);
    ship.transform.Find("ship/colliders/log (2)")?.gameObject.SetActive(value: false);
    ship.transform.Find("ship/colliders/log (3)")?.gameObject.SetActive(value: false);
    UpdateVisual();
    BoxCollider[] colliders = base.transform.GetComponentsInChildren<BoxCollider>();
    m_baseRoot.m_onboardcollider =
      colliders.FirstOrDefault((BoxCollider k) => k.gameObject.name == "OnboardTrigger");
    m_baseRoot.m_onboardcollider.transform.localScale = new Vector3(1f, 1f, 1f);
    m_baseRoot.m_floatcollider = ship.m_floatCollider;
    m_baseRoot.m_floatcollider.transform.localScale = new Vector3(1f, 1f, 1f);
    m_baseRoot.m_blockingcollider = ship.transform.Find("ship/colliders/Cube")
      .GetComponentInChildren<BoxCollider>();
    m_baseRoot.m_blockingcollider.transform.localScale = new Vector3(1f, 1f, 1f);
    m_baseRoot.m_blockingcollider.gameObject.layer =
      ValheimRaftEntrypoint.CustomRaftLayer;
    m_baseRoot.m_blockingcollider.transform.parent.gameObject.layer =
      ValheimRaftEntrypoint.CustomRaftLayer;
    ZLog.Log($"Activating MBRoot: {m_baseRoot.m_id}");
    m_baseRoot.ActivatePendingPiecesCoroutine();
    FirstTimeCreation();
  }

  public void UpdateVisual()
  {
    if (m_nview.m_zdo != null)
    {
      m_flags = (MBFlags)m_nview.m_zdo.GetInt("MBFlags", (int)m_flags);
      base.transform.Find("ship/visual").gameObject.SetActive(!m_flags.HasFlag(MBFlags.HideMesh));
      base.transform.Find("interactive").gameObject.SetActive(!m_flags.HasFlag(MBFlags.HideMesh));
    }
  }

  public void OnDestroy()
  {
    if ((bool)m_baseRoot)
    {
      m_baseRoot.CleanUp();
      UnityEngine.Object.Destroy(m_baseRoot.gameObject);
    }
  }

  private void FirstTimeCreation()
  {
    if (m_baseRoot.GetPieceCount() != 0)
    {
      return;
    }

    GameObject floor = ZNetScene.instance.GetPrefab("wood_floor");
    for (float x = -1f; x < 1.01f; x += 2f)
    {
      for (float z = -2f; z < 2.01f; z += 2f)
      {
        Vector3 pt = base.transform.TransformPoint(new Vector3(x, 0.45f, z));
        GameObject obj = UnityEngine.Object.Instantiate(floor, pt, base.transform.rotation);
        ZNetView netview = obj.GetComponent<ZNetView>();
        m_baseRoot.AddNewPiece(netview);
      }
    }
  }

  internal void Accend()
  {
    if (m_flags.HasFlag(MBFlags.IsAnchored))
    {
      SetAnchor(state: false);
    }

    if (!global::ValheimRAFT.ValheimRaftEntrypoint.Instance.AllowFlight.Value)
    {
      m_targetHeight = 0f;
    }
    else
    {
      if (!m_baseRoot || !m_baseRoot.m_floatcollider)
      {
        return;
      }

      m_targetHeight = Mathf.Clamp(m_baseRoot.m_floatcollider.transform.position.y + 1f,
        ZoneSystem.instance.m_waterLevel, 200f);
    }

    m_nview.m_zdo.Set("MBTargetHeight", m_targetHeight);
  }

  internal void Descent()
  {
    if (m_flags.HasFlag(MBFlags.IsAnchored))
    {
      SetAnchor(state: false);
    }

    float oldTargetHeight = m_targetHeight;
    if (!ValheimRaftEntrypoint.Instance.AllowFlight.Value)
    {
      m_targetHeight = 0f;
    }
    else
    {
      if (!m_baseRoot || !m_baseRoot.m_floatcollider)
      {
        return;
      }

      m_targetHeight = Mathf.Clamp(m_baseRoot.m_floatcollider.transform.position.y - 1f,
        ZoneSystem.instance.m_waterLevel, 200f);
      if (m_baseRoot.m_floatcollider.transform.position.y - 1f <= ZoneSystem.instance.m_waterLevel)
      {
        m_targetHeight = 0f;
      }
    }

    m_nview.m_zdo.Set("MBTargetHeight", m_targetHeight);
  }

  internal void UpdateStats(bool flight)
  {
    if (!m_rigidbody || !m_baseRoot || m_baseRoot.m_statsOverride)
    {
      return;
    }

    m_rigidbody.mass = 3000f;
    m_rigidbody.angularDrag = (flight ? 1f : 0f);
    m_rigidbody.drag = (flight ? 1f : 0f);
    if ((bool)m_ship)
    {
      m_ship.m_angularDamping = (flight ? 5f : 0.8f);
      m_ship.m_backwardForce = 1f;
      m_ship.m_damping = (flight ? 5f : 0.35f);
      m_ship.m_dampingSideway = (flight ? 3f : 0.3f);
      m_ship.m_force = 3f;
      m_ship.m_forceDistance = 5f;
      m_ship.m_sailForceFactor = (flight ? 0.2f : 0.05f);
      m_ship.m_stearForce = (flight ? 0.2f : 1f);
      m_ship.m_stearVelForceFactor = 1.3f;
      m_ship.m_waterImpactDamage = 0f;
      ImpactEffect impact = m_ship.GetComponent<ImpactEffect>();
      if ((bool)impact)
      {
        impact.m_interval = 0.1f;
        impact.m_minVelocity = 0.1f;
        impact.m_damages.m_damage = 100f;
      }
    }
  }

  internal void SetAnchor(bool state)
  {
    m_nview.InvokeRPC("SetAnchor", state);
  }

  public void RPC_SetAnchor(long sender, bool state)
  {
    m_flags = (state ? (m_flags | MBFlags.IsAnchored) : (m_flags & ~MBFlags.IsAnchored));
    m_nview.m_zdo.Set("MBFlags", (int)m_flags);
  }

  internal void SetVisual(bool state)
  {
    m_nview.InvokeRPC("SetVisual", state);
  }

  public void RPC_SetVisual(long sender, bool state)
  {
    m_flags = (state ? (m_flags | MBFlags.HideMesh) : (m_flags & ~MBFlags.HideMesh));
    m_nview.m_zdo.Set("MBFlags", (int)m_flags);
    UpdateVisual();
  }
}