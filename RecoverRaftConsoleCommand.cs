﻿// Decompiled with JetBrains decompiler
// Type: ValheimRAFT.RecoverRaftConsoleCommand
// Assembly: ValheimRAFT, Version=0.0.0.0, Culture=neutral, PublicKeyToken=null
// MVID: B1A8BB6C-BD4E-4881-9FD4-7E1D68B1443D


using Jotunn.Entities;
using System.Collections.Generic;
using UnityEngine;

namespace ValheimRAFT
{
  internal class RecoverRaftConsoleCommand : ConsoleCommand
  {
    public virtual string Name => "RaftRecover";

    public virtual string Help => "Attempts to recover unattached rafts.";

    public virtual void Run(string[] args)
    {
      Collider[] colliderArray =
        Physics.OverlapSphere(((Component)GameCamera.instance).transform.position, 1000f);
      Dictionary<ZDOID, List<ZNetView>> dictionary = new Dictionary<ZDOID, List<ZNetView>>();
      ZLog.Log((object)string.Format("Searching {0}",
        (object)((Component)GameCamera.instance).transform.position));
      foreach (Component component1 in colliderArray)
      {
        ZNetView component2 = component1.GetComponent<ZNetView>();
        if (Object.op_Inequality((Object)component2, (Object)null) && component2.m_zdo != null &&
            !Object.op_Implicit((Object)((Component)component2)
              .GetComponentInParent<MoveableBaseRootComponent>()))
        {
          ZDOID zdoid = component2.m_zdo.GetZDOID(MoveableBaseRootComponent.MBParentHash);
          if (ZDOID.op_Inequality(zdoid, ZDOID.None))
          {
            if (!Object.op_Inequality((Object)ZNetScene.instance.FindInstance(zdoid), (Object)null))
            {
              List<ZNetView> znetViewList;
              if (!dictionary.TryGetValue(zdoid, out znetViewList))
              {
                znetViewList = new List<ZNetView>();
                dictionary.Add(zdoid, znetViewList);
              }

              znetViewList.Add(component2);
            }
          }
          else
            component2.m_zdo.GetVec3(MoveableBaseRootComponent.MBPositionHash, Vector3.zero);
        }
      }

      ZLog.Log((object)string.Format("Found {0} potential ships to recover.",
        (object)dictionary.Count));
      if (args.Length != 0 && args[0] == "confirm")
      {
        foreach (ZDOID key in dictionary.Keys)
        {
          List<ZNetView> znetViewList = dictionary[key];
          MoveableBaseShipComponent component = Object
            .Instantiate<GameObject>(ZNetScene.instance.GetPrefab("MBRaft"),
              ((Component)znetViewList[0]).transform.position,
              ((Component)znetViewList[0]).transform.rotation)
            .GetComponent<MoveableBaseShipComponent>();
          foreach (ZNetView netview in znetViewList)
          {
            ((Component)netview).transform.SetParent(((Component)component.m_baseRoot).transform);
            ((Component)netview).transform.localPosition =
              netview.m_zdo.GetVec3(MoveableBaseRootComponent.MBPositionHash, Vector3.zero);
            ((Component)netview).transform.localRotation =
              netview.m_zdo.GetQuaternion(MoveableBaseRootComponent.MBRotationHash,
                Quaternion.identity);
            component.m_baseRoot.AddNewPiece(netview);
          }
        }
      }
      else if (dictionary.Count > 0)
        ZLog.Log((object)"Use \"RaftRecover confirm\" to complete the recover.");
    }
  }
}