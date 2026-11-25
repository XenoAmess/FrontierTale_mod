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
        private const string FrontSlot = "F_Hair04";
        private const string DirectionSlot = "F_Hair05";
        private const string BackAttachment = "B_Head2";
        private readonly List<Object> allocated = new List<Object>();
        private readonly List<(SkeletonData data, Skin skin)> installedSkins = new List<(SkeletonData, Skin)>();
        private readonly HashSet<SkeletonAnimation> animatedHeads = new HashSet<SkeletonAnimation>();
        private readonly Texture2D frontHead;
        private readonly Texture2D backHead;

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
            Slot face = setup.FindSlot("F_Head");
            if (!(face?.Attachment is MeshAttachment faceMesh))
                return StockHead;
            Rect faceBounds = MeshBounds(face, faceMesh);

            var head = new Skin(CustomHead);
            head.AddSkin(template);
            AtlasRegion frontRegion = null;
            AtlasRegion backRegion = null;
            foreach (Skin.SkinEntry entry in template.Attachments)
            {
                if (!(entry.Attachment is MeshAttachment mesh) || !(mesh.Region is AtlasRegion original))
                    continue;
                Slot slot = setup.Slots.Items[entry.SlotIndex];
                if (entry.Name == FrontSlot)
                {
                    frontRegion = MakeRegion(frontHead, original);
                    head.SetAttachment(entry.SlotIndex, entry.Name, MakeHead(slot, mesh, faceBounds, frontRegion));
                }
                else if (entry.Name == BackAttachment)
                {
                    backRegion = MakeRegion(backHead, original);
                    head.SetAttachment(entry.SlotIndex, entry.Name, MakeHead(slot, mesh, faceBounds, backRegion));
                }
            }

            if (frontRegion == null || backRegion == null)
                return StockHead;

            foreach (Skin.SkinEntry entry in template.Attachments)
            {
                if (entry.Name == FrontSlot || entry.Name == BackAttachment)
                    continue;
                // Replace the stock hair, face and eye meshes with empty geometry.
                // The two new attachments carry the complete head and use the native draw order.
                var empty = new RegionAttachment(entry.Name) { Region = frontRegion };
                empty.UpdateRegion();
                head.SetAttachment(entry.SlotIndex, entry.Name, empty);
            }
            data.Skins.Add(head);
            installedSkins.Add((data, head));
            return CustomHead;
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
            Slot head = skeleton?.FindSlot(FrontSlot);
            if (head?.Attachment == null)
                return;

            // North-facing clips swap the stock hair overlay while leaving the same base head key.
            // Use that marker after Spine has applied the animation timelines.
            bool facingBack = skeleton.FindSlot(DirectionSlot)?.Attachment?.Name == "B_Hair05";
            Attachment replacement = skeleton.Skin?.GetAttachment(head.Data.Index, facingBack ? BackAttachment : FrontSlot);
            if (replacement != null && replacement != head.Attachment)
                head.Attachment = replacement;
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

        private static RegionAttachment MakeHead(Slot slot, MeshAttachment template, Rect faceBounds, AtlasRegion region)
        {
            // Keep the compact bob above the native collar and align its crown to the stock rig.
            float bottom = faceBounds.yMin + faceBounds.height * 0.08f;
            float height = MeshBounds(slot, template).yMax - bottom;
            float width = height * region.width / region.height;
            Bone bone = slot.Bone;
            bone.WorldToLocal(faceBounds.center.x, bottom + height * 0.5f, out float x, out float y);
            var replacement = new RegionAttachment(template.Name)
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
