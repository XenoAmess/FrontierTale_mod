using System;
using System.Collections.Generic;
using System.IO;
using Spine;
using Spine.Unity;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Vivhite
{
    internal sealed class FollowerArt : IDisposable
    {
        private const string StockHead = "Head/Head_NvPu";
        private const string CustomHead = "Head/" + VivhiteMod.HeadSkin;
        internal const string StockCape = "Clothes/Cloth_NvWuDouPeng";
        private const string CustomCape = "Clothes/Cloth_Vivhite";
        private const string FaceSlot = "F_Head";
        private const string HairSlot = "F_Hair04";
        private const string DirectionSlot = "F_Hair05";
        private const string BackAttachment = "B_Head2";
        private readonly List<Object> allocated = new List<Object>();
        private readonly List<(SkeletonData data, Skin skin)> installedSkins = new List<(SkeletonData, Skin)>();
        private readonly HashSet<SkeletonAnimation> animatedHeads = new HashSet<SkeletonAnimation>();
        private readonly Texture2D frontHead;
        private readonly Texture2D backHead;
        private readonly Texture2D frontCape;
        private readonly Texture2D backCape;

        internal Sprite Avatar { get; }
        internal Sprite Portrait { get; }

        internal FollowerArt(string folder)
        {
            try
            {
                Avatar = ReadSprite(folder, "avatar.png");
                Portrait = ReadSprite(folder, "portrait.png");
                frontHead = ReadTexture(folder, "head-front.png");
                backHead = ReadTexture(folder, "head-back.png");
                frontCape = ReadTexture(folder, "cape-front.png");
                backCape = ReadTexture(folder, "cape-back.png");
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        private Texture2D ReadTexture(string folder, string name)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            allocated.Add(texture);
            if (!ImageConversion.LoadImage(texture, File.ReadAllBytes(Path.Combine(folder, name)), markNonReadable: true))
                throw new InvalidDataException("Could not read Vivhite image " + name);
            texture.name = "Vivhite/" + name;
            texture.wrapMode = UnityEngine.TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            return texture;
        }

        private Sprite ReadSprite(string folder, string name)
        {
            Texture2D texture = ReadTexture(folder, name);
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = "Vivhite/" + name;
            allocated.Add(sprite);
            return sprite;
        }

        internal string GetHeadSkin(Skeleton skeleton)
        {
            if (skeleton == null)
                return StockHead;

            SkeletonData data = skeleton.Data;
            if (data.FindSkin(CustomHead) != null)
                return CustomHead;

            Skin template = data.FindSkin(StockHead);
            if (template == null)
                return StockHead;

            var setup = new Skeleton(data);
            setup.SetSkin(template);
            setup.SetSlotsToSetupPose();
            setup.UpdateWorldTransform(Skeleton.Physics.Pose);
            Slot face = setup.FindSlot(FaceSlot);
            Slot hair = setup.FindSlot(HairSlot);
            if (!(face?.Attachment is MeshAttachment faceMesh) || !(hair?.Attachment is MeshAttachment hairMesh) ||
                !(faceMesh.Region is AtlasRegion stockFace) || !(hairMesh.Region is AtlasRegion stockHair))
                return StockHead;
            Rect faceBounds = MeshBounds(face, faceMesh);
            float crown = MeshBounds(hair, hairMesh).yMax;
            var head = new Skin(CustomHead);
            head.AddSkin(template);
            AtlasRegion frontRegion = MakeRegion(frontHead, stockFace);
            AtlasRegion backRegion = MakeRegion(backHead, stockHair);
            foreach (Skin.SkinEntry entry in template.Attachments)
            {
                RegionAttachment replacement;
                if (entry.Name == FaceSlot)
                    replacement = MakeHead(face, faceBounds, crown, frontRegion);
                else if (entry.Name == BackAttachment)
                    replacement = MakeHead(hair, faceBounds, crown, backRegion);
                else
                {
                    replacement = new RegionAttachment(entry.Name) { Region = frontRegion };
                    replacement.UpdateRegion();
                }
                head.SetAttachment(entry.SlotIndex, entry.Name, replacement);
            }
            AddSkin(data, head);
            return CustomHead;
        }

        internal string GetCapeSkin(Skeleton skeleton)
        {
            var data = skeleton?.Data;
            if (data == null)
                return StockCape;
            if (data.FindSkin(CustomCape) != null)
                return CustomCape;
            Skin template = data.FindSkin(StockCape);
            if (template == null)
                return StockCape;

            var setup = new Skeleton(data);
            setup.SetSkin(template);
            setup.SetSlotsToSetupPose();
            setup.UpdateWorldTransform(Skeleton.Physics.Pose);
            var cape = new Skin(CustomCape);
            cape.AddSkin(template);
            foreach (Skin.SkinEntry entry in template.Attachments)
            {
                Slot slot = setup.Slots.Items[entry.SlotIndex];
                if (slot.Data.Name != "YiFu" || !(entry.Attachment is MeshAttachment mesh) || !(mesh.Region is AtlasRegion original))
                    continue;
                bool rear = entry.Name.EndsWith("_B", StringComparison.Ordinal);
                AtlasRegion region = MakeRegion(rear ? backCape : frontCape, original);
                Rect bounds = MeshBounds(slot, mesh);
                float width = bounds.width * 1.10f;
                float height = bounds.height * 1.12f;
                cape.SetAttachment(entry.SlotIndex, entry.Name,
                    Attach(slot.Bone, entry.Name, region, bounds.center.x, bounds.yMin + height * 0.5f, width, height));
            }
            AddSkin(data, cape);
            return CustomCape;
        }

        private void AddSkin(SkeletonData data, Skin skin)
        {
            data.Skins.Add(skin);
            installedSkins.Add((data, skin));
        }

        internal void BindDirection(SkeletonAnimation animation)
        {
            if (animation == null || animatedHeads.Contains(animation))
                return;
            animatedHeads.RemoveWhere(head => head == null);
            animatedHeads.Add(animation);
            animation.UpdateComplete += ApplyDirection;
            ApplyDirection(animation);
        }

        private static void ApplyDirection(ISkeletonAnimation animation)
        {
            Skeleton skeleton = animation.Skeleton;
            Slot face = skeleton?.FindSlot(FaceSlot);
            Slot hair = skeleton?.FindSlot(HairSlot);
            if (face == null || hair == null)
                return;

            // Use the rig's direction marker. The face sits in front of collars, while back hair
            // remains in the stock rear layer so cloaks and equipped chest pieces can overlap it.
            bool facingBack = skeleton.FindSlot(DirectionSlot)?.Attachment?.Name == "B_Hair05";
            Attachment visibleFace = skeleton.Skin?.GetAttachment(face.Data.Index, FaceSlot);
            Attachment visibleHair = skeleton.Skin?.GetAttachment(hair.Data.Index, facingBack ? BackAttachment : HairSlot);
            if (visibleFace is RegionAttachment && visibleHair is RegionAttachment)
            {
                face.Attachment = facingBack ? null : visibleFace;
                hair.Attachment = visibleHair;
            }
        }

        private AtlasRegion MakeRegion(Texture2D texture, AtlasRegion original)
        {
            var material = new Material((Material)original.page.rendererObject)
            {
                name = texture.name,
                mainTexture = texture
            };
            if (material.HasProperty("_StraightAlphaInput"))
                material.SetFloat("_StraightAlphaInput", 1f);
            allocated.Add(material);
            return new AtlasRegion
            {
                name = texture.name,
                page = new AtlasPage { rendererObject = material, width = texture.width, height = texture.height },
                width = texture.width,
                height = texture.height,
                originalWidth = texture.width,
                originalHeight = texture.height,
                u = 0f,
                v = 1f,
                u2 = 1f,
                v2 = 0f
            };
        }

        private static Rect MeshBounds(Slot slot, MeshAttachment mesh)
        {
            var vertices = new float[mesh.WorldVerticesLength];
            mesh.ComputeWorldVertices(slot, 0, vertices.Length, vertices, 0, 2);
            float left = float.PositiveInfinity;
            float right = float.NegativeInfinity;
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            for (int i = 0; i < vertices.Length; i += 2)
            {
                left = Mathf.Min(left, vertices[i]);
                right = Mathf.Max(right, vertices[i]);
                bottom = Mathf.Min(bottom, vertices[i + 1]);
                top = Mathf.Max(top, vertices[i + 1]);
            }
            return Rect.MinMaxRect(left, bottom, right, top);
        }

        private static RegionAttachment MakeHead(Slot slot, Rect faceBounds, float crown, AtlasRegion region)
        {
            float bottom = faceBounds.yMin + faceBounds.height * 0.02f;
            float height = crown - bottom;
            float width = height * region.width / region.height;
            return Attach(slot.Bone, slot.Data.Name, region, faceBounds.center.x, bottom + height * 0.5f, width, height);
        }

        private static RegionAttachment Attach(Bone bone, string name, AtlasRegion region, float centerX, float centerY,
            float width, float height)
        {
            bone.WorldToLocal(centerX, centerY, out float x, out float y);
            var replacement = new RegionAttachment(name)
            {
                Region = region,
                X = x,
                Y = y,
                Width = width / bone.WorldScaleX,
                Height = height / bone.WorldScaleY,
                Rotation = -bone.WorldRotationX
            };
            replacement.UpdateRegion();
            return replacement;
        }

        public void Dispose()
        {
            foreach (SkeletonAnimation animation in animatedHeads)
                if (animation != null)
                    animation.UpdateComplete -= ApplyDirection;
            animatedHeads.Clear();
            foreach ((SkeletonData data, Skin skin) in installedSkins)
                data.Skins.Remove(skin);
            installedSkins.Clear();
            foreach (Object item in allocated)
                Object.Destroy(item);
            allocated.Clear();
        }
    }
}
