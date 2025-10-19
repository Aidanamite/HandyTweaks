using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using UnityEngine;

namespace HandyTweaks
{
    public abstract class ExtendedClass<X, Y> where X : ExtendedClass<X, Y>, new() where Y : class
    {
        static ConditionalWeakTable<Y, X> table = new ConditionalWeakTable<Y, X>();
        public static X Get(Y instance)
        {
            if (table.TryGetValue(instance, out var v))
            {
                v.OnGet(instance);
                return v;
            }
            v = new X();
            table.Add(instance, v);
            v.OnCreate(instance);
            v.OnGet(instance);
            return v;
        }
        protected virtual void OnCreate(Y instance) { }
        protected virtual void OnGet(Y instance) { }
    }

    public class ExtendedAmmo : ExtendedClass<ExtendedAmmo, ObAmmo>
    {
        public WeaponManager manager;

        static MaterialPropertyBlock props = new MaterialPropertyBlock();
        public static GameObject EditColors(GameObject obj, ObAmmo fireball)
        {
            if (!fireball)
                return obj;
            var manager = Get(fireball).manager;
            if (!manager || !manager.IsLocal || !(manager is PetWeaponManager p) || !p.SanctuaryPet)
                return obj;
            var d = ExtendedPetData.Get(p.SanctuaryPet.pData);
            if (d.FireballColor == null)
                return obj;
            var color = d.FireballColor.Value;
            //Debug.Log($"Changing fireball {obj.name} to {color}");
            foreach (var r in obj.GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(props);
                foreach (var m in r.sharedMaterials)
                    if (m && m.shader)
                    {
                        var c = m.shader.GetPropertyCount();
                        for (var i = 0; i < c; i++)
                            if (m.shader.GetPropertyType(i) == UnityEngine.Rendering.ShaderPropertyType.Color)
                            {
                                var n = m.shader.GetPropertyNameId(i);
                                props.SetColor(n, m.GetColor(n).Shift(color));
                            }
                    }
                r.SetPropertyBlock(props);
            }
            foreach (var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
            {
                var m = ps.main;
                m.startColor = m.startColor.Shift(color);
                var s = ps.colorBySpeed;
                s.color = s.color.Shift(color);
                var l = ps.colorOverLifetime;
                l.color = l.color.Shift(color);
            }
            return obj;
        }
    }

    public class ExtendedPetData : ExtendedClass<ExtendedPetData, RaisedPetData>
    {
        public const string FIREBALLCOLOR_OLDKEY = "HTFC"; // Handy Tweaks Fireball Colour (non-hexidecimal, legacy purposes only)
        public const string FIREBALLCOLOR_KEY = "HTFCH"; // Handy Tweaks Fireball Colour
        public Color? FireballColor;
        public const string EMISSIONCOLOR_OLDKEY = "HTEC"; // Handy Tweaks Emission Colour (non-hexidecimal, legacy purposes only)
        public const string EMISSIONCOLOR_KEY = "HTECH"; // Handy Tweaks Emission Colour
        public Color? EmissionColor;
        public const string ISINTACT_KEY = "HTIFI"; // Handy Tweaks Is Fury Intact
        public bool isIntact;
        public const string HIGHERCOLORS_KEY = "HTHCV"; // Using "higher" colours for dragon
    }

    public class ExtendedEmissionTexture : ExtendedClass<ExtendedEmissionTexture, Texture2D>
    {
        public bool HasAnyColor;
        protected override void OnCreate(Texture2D instance)
        {
            base.OnCreate(instance);
            HasAnyColor = instance.GetPixelsSafe().Any(x => x.r != 0 || x.g != 0 || x.b != 0);
        }
    }

    public class ExtendedDragonCustomization : ExtendedClass<ExtendedDragonCustomization, UiDragonCustomization>
    {
        public static Color NullColorFallback = new Color(0, 0, 0, 0.5f);
        UiDragonCustomization ui;
        public KAWidget emissionColorBtn;
        public KAWidget fireballColorBtn;
        public KAToggleButton ToggleBtnRepaired;
        public Color? emissionColor;
        public Color? fireballColor;
        protected override void OnCreate(UiDragonCustomization instance)
        {
            ui = instance;
            var e = ExtendedPetData.Get(instance.pPetData);
            emissionColor = e.EmissionColor;
            fireballColor = e.FireballColor;
            var p1 = (Vector2)instance.mPrimaryColorBtn.GetPosition();
            var p2 = (Vector2)instance.mSecondaryColorBtn.GetPosition();
            var p3 = (Vector2)instance.mTertiaryColorBtn.GetPosition();
            var p4 = p2.Rotate(-60 * Mathf.Deg2Rad, p1);
            var p5 = p3.Rotate(-60 * Mathf.Deg2Rad, p2);

            emissionColorBtn = instance.DuplicateWidget(instance.mPrimaryColorBtn, instance.mPrimaryColorBtn.pAnchor.side);
            emissionColorBtn.transform.SetParent(instance.mPrimaryColorBtn.transform.parent);
            emissionColorBtn.SetPosition(p4.x, p4.y);
            emissionColorBtn.SetVisibility(true);
            emissionColorBtn.SetState(KAUIState.INTERACTIVE);

            fireballColorBtn = instance.DuplicateWidget(instance.mPrimaryColorBtn, instance.mPrimaryColorBtn.pAnchor.side);
            fireballColorBtn.transform.SetParent(instance.mPrimaryColorBtn.transform.parent);
            fireballColorBtn.SetPosition(p5.x, p5.y);
            fireballColorBtn.SetVisibility(true);
            fireballColorBtn.SetState(KAUIState.INTERACTIVE);

            if (!instance.mIsCreationUI && !(string.IsNullOrEmpty(instance.pPetData.FindAttrData("_LastCustomizedStage")?.Value)))
            {
                var o = -instance.mToggleBtnMale.pBackground.height * 1.5f;
                var p = instance.mToggleBtnMale.GetPosition();
                instance.mToggleBtnMale.SetPosition(p.x + o, p.y + o);
                p = instance.mToggleBtnFemale.GetPosition();
                instance.mToggleBtnFemale.SetPosition(p.x + o, p.y + o);
            }

            emissionColorBtn.SetText("Glow");
            emissionColorBtn.pBackground.color = emissionColor ?? NullColorFallback;
            fireballColorBtn.SetText("Fireball");
            fireballColorBtn.pBackground.color = fireballColor ?? NullColorFallback;

            if (!instance.mIsCreationUI && instance.mUiJournalCustomization && MeshConversion.ShouldAffect(instance.mPetData.PetTypeID))
            {
                var b = ToggleBtnRepaired = (KAToggleButton)instance.DuplicateWidget(instance.mToggleBtnFemale, instance.mToggleBtnFemale.pAnchor.side);
                b.name = "IntactNightfuries.ToggleBtnRepaired";
                instance.mToggleBtnFemale.pParentWidget?.AddChild(b);
                var icon = b.transform.Find("Icon").GetComponent<UISlicedSprite>();
                icon.spriteName = icon.pOrgSprite = "IcoDWDragonsJournalDecals";
                b._Grouped = false;
                b.mToggleButtons = new KAToggleButton[0];
                b.mCachedTooltipInfo._Text = new LocaleString("Toothless Tail");
                b._CheckedTooltipInfo._Text = new LocaleString("Natural Tail");
                foreach (var a in b._CheckedInfo._ColorInfo._ApplyTo)
                    a._Color = new Color(0.1f, 0.9f, 0.1f);
                var p = instance.mUiJournalCustomization.mAvatarBtn.GetPosition();
                b.SetPosition(p.x - (b.pBackground.width + instance.mUiJournalCustomization.mAvatarBtn.pBackground.width) * 0.65f, p.y);
                b.SetChecked(b._StartChecked = ExtendedPetData.Get(instance.mPetData).isIntact);
            }
        }

        public static Color GetSelectedColor(Color fallback, UiDragonCustomization instance)
        {
            var e = Get(instance);
            if (instance.mSelectedColorBtn == e.emissionColorBtn)
                return e.emissionColor ?? Color.black;
            if (instance.mSelectedColorBtn == e.fireballColorBtn)
                return e.fireballColor ?? Color.black;
            return fallback;
        }

        public static bool FreeCustomization(bool fallback, UiDragonCustomization instance)
        {
            if (fallback)
                return true;
            var e = Get(instance);
            return instance.mSelectedColorBtn == e.emissionColorBtn || instance.mSelectedColorBtn == e.fireballColorBtn;
        }

        public static UiDragonCustomization OnPaletteClick(UiDragonCustomization instance, Color color)
        {
            var e = Get(instance);
            if (instance.mSelectedColorBtn == e.emissionColorBtn)
            {
                e.emissionColor = color;
                instance.mRebuildTexture = true;
            }
            else if (instance.mSelectedColorBtn == e.fireballColorBtn)
            {
                e.fireballColor = color;
                instance.mRebuildTexture = true;
            }
            ColorPicker.TrySetColor(color);
            return instance;
        }

        public static void StoreValues(SanctuaryPet pet, Color a, Color b, Color c, bool save, UiDragonCustomization ui)
        {
            var uie = Get(ui);
            var pe = ExtendedPetData.Get(ui.pPetData);
            pe.EmissionColor = uie.emissionColor;
            pe.FireballColor = uie.fireballColor;
            pe = ExtendedPetData.Get(pet.pData);
            pe.EmissionColor = uie.emissionColor;
            pe.FireballColor = uie.fireballColor;
            pet.SetColors(a, b, c, save);
        }
    }

    public class ExtendedInfoCard : ExtendedClass<ExtendedInfoCard, UiDragonsInfoCardItem>
    {
        public UiDragonsInfoCardItem instance;
        public KAWidget ReleaseBtn;
        protected override void OnCreate(UiDragonsInfoCardItem instance)
        {
            base.OnCreate(instance);
            this.instance = instance;
            if (instance.mBtnChangeName)
            {
                ReleaseBtn = instance.pUI.DuplicateWidget(instance.mBtnChangeName, instance.mBtnChangeName.pAnchor.side);
                instance.mBtnChangeName.pParentWidget?.AddChild(ReleaseBtn);
                ReleaseBtn.transform.position = instance.mBtnChangeName.transform.position + new Vector3(-50, 0, 0);
                ReleaseBtn.SetToolTipText("Release Dragon");
                ReleaseBtn.RemoveChildItem(ReleaseBtn.FindChildItem("Gems"), true);
                var back = ReleaseBtn.FindChildItem("Icon").transform.Find("Background").GetComponent<UISprite>();
                back.spriteName = "IconIgnore";
                back.pOrgSprite = "IconIgnore";
                back = ReleaseBtn.transform.Find("Background").GetComponent<UISprite>();
                back.pOrgColorTint = back.pOrgColorTint.Shift(Color.red);
                back.color = back.color.Shift(Color.red);
            }
        }
        public void Refresh()
        {
            if (ReleaseBtn)
                ReleaseBtn.SetVisibility(instance.pSelectedPetID != SanctuaryManager.pCurPetData.RaisedPetID);
        }
        public void OnClick(KAWidget widget)
        {
            if (ReleaseBtn && widget == ReleaseBtn)
            {
                var selected = instance.pSelectedPetID;
                var instances = Resources.FindObjectsOfTypeAll<SanctuaryPet>().Where(x => x.pData?.RaisedPetID == selected).ToArray();
                Main.TryDestroyDragon(instance, () =>
                {
                    foreach (var i in instances)
                        if (i)
                            Object.Destroy(i.gameObject);
                    var info = instance.mMsgObject.GetComponent<UiDragonsInfoCard>();
                    var list = Object.FindObjectOfType<UiDragonsListCard>();
                    if (list && list.GetVisibility())
                    {
                        list.RefreshUI();
                        list.SelectDragon(SanctuaryManager.pCurPetData?.RaisedPetID ?? 0);
                        info.RefreshUI();
                    }
                    else
                    {
                        info.PopOutCard();
                        Object.FindObjectOfType<UiStablesInfoCard>()?.RefreshUI();
                    }
                });
            }
        }
    }

    public class MeshConversion : ExtendedClass<MeshConversion, Mesh>
    {
        public Mesh Other;
        public bool IsGenerated = false;
        static bool Disable = false;
        static Dictionary<int, int> mirrorBones = new Dictionary<int, int> { { 2, 5 }, { 5, 2 }, { 3, 6 }, { 6, 3 }, { 4, 7 }, { 7, 4 }, { 15, 17 }, { 17, 15 }, { 16, 18 }, { 18, 16 }, { 19, 21 }, { 21, 19 }, { 20, 22 }, { 22, 20 }, { 29, 30 }, { 30, 29 }, { 34, 36 }, { 36, 34 }, { 35, 37 }, { 37, 35 }, { 38, 46 }, { 46, 38 }, { 39, 47 }, { 47, 39 }, { 40, 48 }, { 48, 40 }, { 41, 49 }, { 49, 41 }, { 42, 50 }, { 50, 42 }, { 43, 51 }, { 51, 43 }, { 44, 52 }, { 52, 44 }, { 45, 53 }, { 53, 45 }, { 54, 56 }, { 56, 54 }, { 55, 57 }, { 57, 55 }, { 59, 62 }, { 62, 59 }, { 60, 63 }, { 63, 60 }, { 61, 64 }, { 64, 61 }, { 65, 90 }, { 90, 65 }, { 66, 91 }, { 91, 66 }, { 67, 92 }, { 92, 67 }, { 68, 93 }, { 93, 68 }, { 69, 94 }, { 94, 69 }, { 70, 95 }, { 95, 70 }, { 71, 96 }, { 96, 71 }, { 72, 97 }, { 97, 72 }, { 73, 98 }, { 98, 73 }, { 74, 99 }, { 99, 74 }, { 75, 100 }, { 100, 75 }, { 76, 101 }, { 101, 76 }, { 77, 102 }, { 102, 77 }, { 78, 103 }, { 103, 78 }, { 79, 104 }, { 104, 79 }, { 80, 105 }, { 105, 80 }, { 81, 106 }, { 106, 81 }, { 82, 107 }, { 107, 82 }, { 83, 108 }, { 108, 83 }, { 84, 109 }, { 109, 84 }, { 85, 110 }, { 110, 85 }, { 86, 111 }, { 111, 86 }, { 87, 112 }, { 112, 87 }, { 88, 113 }, { 113, 88 }, { 89, 114 }, { 114, 89 } };

        protected override void OnGet(Mesh instance)
        {
            base.OnGet(instance);
            if (Disable || Other)
                return;
            try
            {
                if (!instance.isReadable)
                {
                    Main.logger.LogError("Mesh must have read/write enabled >> " + instance.name);
                    return;
                }

                var keep = new Dictionary<int, bool>();

                var tDup = new List<int>();
                var tKeep = new List<int>();
                var verts = instance.vertices;
                var tris = instance.triangles;

                Side GetSide(int vInd) => verts[vInd].x >= 0 ? Side.Good : verts[vInd].x < -0.001 ? Side.Bad : Side.Middle;
                for (int i = 0; i < tris.Length; i += 3)
                {
                    var t1 = tris[i];
                    var t2 = tris[i + 1];
                    var t3 = tris[i + 2];
                    var s1 = GetSide(t1);
                    var s2 = GetSide(t2);
                    var s3 = GetSide(t3);
                    if (s1 == Side.Good || s2 == Side.Good || s3 == Side.Good || (s1 == Side.Middle && s2 == Side.Middle && s3 == Side.Middle))
                    {
                        var notAllBad = s1 != Side.Bad || s2 != Side.Bad || s3 != Side.Bad;
                        keep[t1] = (keep.TryGetValue(t1,out var v) ? v : false) || notAllBad;
                        keep[t2] = (keep.TryGetValue(t2, out v) ? v : false) || notAllBad;
                        keep[t3] = (keep.TryGetValue(t3, out v) ? v : false) || notAllBad;
                        if (notAllBad)
                            tDup.Add(i);
                        else
                            tKeep.Add(i);
                    }
                }
                var uv = instance.uv;
                var norms = instance.normals;
                var tangs = instance.tangents;
                var bones = GetBoneWeights(instance);
                var nVerts = new List<Vector3>(verts.Length * 2);
                var nUV = new List<Vector2>(uv.Length * 2);
                var nNorms = new List<Vector3>(norms.Length * 2);
                var nTangs = tangs.Length == 0 ? null : new List<Vector4>(verts.Length * 2);
                var nBones = new List<List<BoneWeight1>>(bones.Count * 2);
                var indRemap = new Dictionary<int, int>();
                for (int i = 0; i < verts.Length; i++)
                    if (keep.TryGetValue(i, out var dup))
                    {
                        indRemap[i] = indRemap.Count;
                        nVerts.Add(verts[i]);
                        nUV.Add(uv[i]);
                        nNorms.Add(norms[i]);
                        nTangs?.Add(tangs.GetSafe(i));
                        nBones.Add(bones[i]);
                        if (dup)
                        {
                            indRemap[i + verts.Length] = indRemap.Count;
                            nVerts.Add(Mirror(verts[i]));
                            nUV.Add(uv[i]);
                            nNorms.Add(Mirror(norms[i]));
                            nTangs?.Add(Mirror(tangs.GetSafe(i)));
                            nBones.Add(bones[i].Select(x => new BoneWeight1() { boneIndex = mirrorBones.TryGetValue(x.boneIndex, out var y) ? y : x.boneIndex, weight = x.weight }).ToList());
                        }
                    }
                var subs = GetSubmeshes(instance);
                var nTris = new List<int>();
                var nSubs = new List<(int start, int count)>();
                foreach (var s in subs)
                {
                    var count = 0;
                    foreach (var i in tKeep)
                        if (i >= s.start && i < s.start + s.count)
                        {
                            nTris.Add(indRemap[tris[i]]);
                            nTris.Add(indRemap[tris[i + 1]]);
                            nTris.Add(indRemap[tris[i + 2]]);
                            count += 3;
                        }
                    foreach (var i in tDup)
                        if (i >= s.start && i < s.start + s.count)
                        {
                            nTris.Add(indRemap[tris[i]]);
                            nTris.Add(indRemap[tris[i + 1]]);
                            nTris.Add(indRemap[tris[i + 2]]);
                            nTris.Add(indRemap.TryGetValue(tris[i] + verts.Length, out var ni) ? ni : indRemap[tris[i]]);
                            nTris.Add(indRemap.TryGetValue(tris[i + 2] + verts.Length, out var ni2) ? ni2 : indRemap[tris[i + 2]]);
                            nTris.Add(indRemap.TryGetValue(tris[i + 1] + verts.Length, out var ni1) ? ni1 : indRemap[tris[i + 1]]);
                            count += 6;
                        }
                    nSubs.Add((nSubs.Count != 0 ? nSubs[nSubs.Count - 1].start + nSubs[nSubs.Count - 1].count : 0, count));
                }
                var nm = new Mesh();
                nm.name = "Intact" + instance.name;
                nm.vertices = nVerts.ToArray();
                nm.uv = nUV.ToArray();
                nm.normals = nNorms.ToArray();
                if (nTangs != null)
                    nm.tangents = nTangs.ToArray();
                SetBoneWeights(nm, nBones);
                nm.triangles = nTris.ToArray();
                SetSubmeshes(nm, nSubs);
                nm.bindposes = instance.bindposes;
                nm.RecalculateBounds();

                Disable = true;
                var other = Get(nm);
                Other = nm;
                other.Other = instance;
                other.IsGenerated = true;
            }
            finally
            {
                Disable = false;
            }
        }

        static Vector3 Mirror(Vector3 v) => new Vector3(-v.x, v.y, v.z);
        static Vector4 Mirror(Vector4 v) => new Vector4(-v.x, v.y, v.z, -v.w);

        static List<List<BoneWeight1>> GetBoneWeights(Mesh mesh)
        {
            using (var all = mesh.GetAllBoneWeights())
            using (var per = mesh.GetBonesPerVertex())
            {
                var r = new List<List<BoneWeight1>>();
                var cur = 0;
                foreach (var n in per)
                {
                    var l = new List<BoneWeight1>();
                    r.Add(l);
                    for (int o = 0; o < n; o++)
                        l.Add(all[o + cur]);
                    cur += n;
                }
                return r;
            }
        }

        static void SetBoneWeights(Mesh mesh, List<List<BoneWeight1>> weights)
        {
            var per = new List<byte>();
            var all = new List<BoneWeight1>();
            foreach (var l in weights)
            {
                per.Add((byte)l.Count);
                all.AddRange(l);
            }
            using (var per2 = new NativeArray<byte>(per.ToArray(), Allocator.Temp))
            using (var all2 = new NativeArray<BoneWeight1>(all.ToArray(), Allocator.Temp))
                mesh.SetBoneWeights(per2, all2);
        }

        static List<(int start, int count)> GetSubmeshes(Mesh mesh)
        {
            if (mesh.subMeshCount <= 0)
                return new List<(int, int)>();
            var l = new List<(int, int)>();
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                var s = mesh.GetSubMesh(i);
                l.Add((s.indexStart, s.indexCount));
            }
            return l;
        }

        static void SetSubmeshes(Mesh mesh, List<(int start, int count)> submeshes)
        {
            if (submeshes == null)
                mesh.SetSubMeshes(new UnityEngine.Rendering.SubMeshDescriptor[0], ~UnityEngine.Rendering.MeshUpdateFlags.Default);
            else
                mesh.SetSubMeshes(submeshes.Select(x => new UnityEngine.Rendering.SubMeshDescriptor(x.start, x.count)).ToArray(), ~UnityEngine.Rendering.MeshUpdateFlags.Default);
        }

        public static void EnforceModel(RaisedPetData pet, IEnumerable<SkinnedMeshRenderer> renderers)
        {
            if (ShouldAffect(pet.PetTypeID))
                foreach (var r in renderers)
                    if (r && r.name == "NightFuryMesh" && r.sharedMesh)
                    {
                        var d = Get(r.sharedMesh);
                        if (d.Other && ExtendedPetData.Get(pet).isIntact != d.IsGenerated)
                        {
                            r.sharedMesh = d.Other;
                        }
                    }
        }

        static int _nightfury;
        public static bool ShouldAffect(int TypeId)
        {
            if (_nightfury == 0)
                _nightfury = SanctuaryData.FindSanctuaryPetTypeInfo("NightFury")._TypeID;
            return TypeId == _nightfury;
        }
    }
}