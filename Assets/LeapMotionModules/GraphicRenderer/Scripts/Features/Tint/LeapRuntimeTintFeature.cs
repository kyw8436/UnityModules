/******************************************************************************
 * Copyright (C) Leap Motion, Inc. 2011-2017.                                 *
 * Leap Motion proprietary and  confidential.                                 *
 *                                                                            *
 * Use subject to the terms of the Leap Motion SDK Agreement available at     *
 * https://developer.leapmotion.com/sdk_agreement, or another agreement       *
 * between Leap Motion and you, your company or other organization.           *
 ******************************************************************************/

using System;
using UnityEngine;

namespace Leap.Unity.GraphicalRenderer {

  [LeapGraphicTag("Tint")]
  [Serializable]
  public class LeapRuntimeTintFeature : LeapGraphicFeature<LeapRuntimeTintData> {
    public const string FEATURE_NAME = LeapGraphicRenderer.FEATURE_PREFIX + "TINTING";
  }
}
