﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace InnoVault.TileProcessors
{
    /// <summary>
    /// 物块处理器，简称TP实体，它的功能目标类似于<see cref="TileEntity"/>
    /// </summary>
    public abstract class TileProcessor
    {
        #region Data
        /// <summary>
        /// 目标物块ID，一般标记<see cref="Tile.TileType"/>的值
        /// 如果返回0，那么这个实体就不会被系统放置，这意味着你不应该尝试给所有泥土提供一个实体，因为泥土的图格ID是0，但是<see cref="ModContent.TileType{T}"/>的失败返回也是0
        /// </summary>
        public virtual int TargetTileID => -1;
        /// <summary>
        /// 这个物块处理器来自什么模组
        /// </summary>
        public Mod Mod => TileProcessorLoader.TP_Type_To_Mod[GetType()];
        /// <summary>
        /// 显示这个实例的填装名，如InnoVault:TileProcessor
        /// </summary>
        public string LoadenName => Mod.Name + ":" + GetType().Name;
        /// <summary>
        /// 这个模块所要跟随的物块结构，如果对象是一个多结构物块，那么这个块一般代表左上角
        /// 这个值只在模块加载或被放置时更新一次
        /// 在客户端上，这个值可能并没有加载，在使用时需要考虑环境验证或者编写一些防御代码，防止出现一些意料之外的情况
        /// </summary>
        public Tile Tile = default;
        /// <summary>
        /// 如果是玩家中途手动放置的物块所生成的模块，这个值会标记为放置所用的物品
        /// ，否则为<see langword="null"/>，比如进入世界初始化生成的模块
        /// </summary>
        public Item TrackItem;
        /// <summary>
        /// 在<see cref="Initialize"/>调用后被设置为<see langword="true"/>
        /// </summary>
        public bool Spwan;
        /// <summary>
        /// 模块的活跃性，如果是<see langword="false"/>，那么模块将不再进行更新和绘制
        /// ，其在列表<see cref="TileProcessorLoader.TP_InWorld"/>中的实例引用将随时可能被顶替为新的模块
        /// </summary>
        public bool Active;
        /// <summary>
        /// 关于多人模式下，玩家进入世界时是否要请求这个TP实例进行网络响应，默认为<see langword="true"/>
        /// </summary>
        public bool LoadenWorldSendData = true;
        /// <summary>
        /// 玩家鼠标是否悬停在TP实体之上
        /// </summary>
        public bool HoverTP;
        /// <summary>
        /// 这个Tp实体是否在玩家的画面内，该值在绘制函数中实时更新
        /// </summary>
        public bool InScreen;
        /// <summary>
        /// 这个模块在世界中的唯一标签，如果实体不再活跃，它将随时被新加入的模块顶替，顶替的模块将继续使用相同WhoAmI值
        /// 注意，WhoAmI的值在各个端上都可能是不一致的，WhoAmI只应当做本地端的索引使用
        /// </summary>
        public int WhoAmI;
        /// <summary>
        /// 实体的ID，在游戏加载时会给每个实体分配一个唯一的ID值
        /// </summary>
        public int ID => TileProcessorLoader.TP_Type_To_ID[GetType()];
        /// <summary>
        /// 宽度，默认为16，如果<see cref="Tile"/>存在，则会在初始化时自动设置为其宽度，注意，单位是像素，比如如果一个多结构物块有两个物块宽，这个值会是32
        /// </summary>
        public int Width = 16;
        /// <summary>
        /// 高度，默认为16，如果<see cref="Tile"/>存在，则会在初始化时自动设置为其高度，注意，单位是像素，比如如果一个多结构物块有两个物块高，这个值会是32
        /// </summary>
        public int Height = 16;
        /// <summary>
        /// 这个实体在屏幕上绘制的扩张范围，默认为160
        /// </summary>
        public int DrawExtendMode = 160;
        /// <summary>
        /// 单位为像素，玩家超出此距离后，实体将停止更新以节省性能。默认为 -1，即不启用
        /// </summary>
        public int IdleDistance = -1;
        /// <summary>
        /// 这个TP实体的碰撞矩形
        /// </summary>
        public virtual Rectangle HitBox => PosInWorld.GetRectangle(Size);
        /// <summary>
        /// 矩形大小
        /// </summary>
        public Vector2 Size => new Vector2(Width, Height);
        /// <summary>
        /// 这个模块在世界物块坐标系上的位置，通常等价于所跟随的物块的坐标
        /// </summary>
        public Point16 Position;
        /// <summary>
        /// 这个模块在世界实体坐标系上的位置
        /// </summary>
        public Vector2 PosInWorld => new Vector2(Position.X, Position.Y) * 16;
        /// <summary>
        /// 这个模块在世界实体坐标系上的中心位置
        /// </summary>
        public Vector2 CenterInWorld => PosInWorld + Size / 2;
        #endregion
        /// <summary>
        /// 克隆函数，如果制作的模块类型含有独立的字段，一般要重写克隆函数复制这些字段
        /// </summary>
        /// <returns></returns>
        public virtual TileProcessor Clone() => (TileProcessor)Activator.CreateInstance(GetType());

        /// <summary>
        /// 这个TP实体的网络克隆行为，如果存在特殊的自定义字段，重写这个方法并进行适当的额外克隆以保证网络更新下实体保持正常
        /// </summary>
        /// <param name="writer"></param>
        public virtual void NetCloneSend(ref ModPacket writer) {
            writer.Write(Active);
            writer.Write(WhoAmI);
            writer.WritePoint16(Position);
        }

        /// <summary>
        /// 接受网络TP实体克隆的信息，对应<see cref="NetCloneSend"/>方法的处理，务必保证消息的接收是对应的
        /// </summary>
        /// <param name="reader"></param>
        public virtual void NetCloneRead(BinaryReader reader) {
            Active = reader.ReadBoolean();
            WhoAmI = reader.ReadInt32();
            Position = reader.ReadPoint16();
        }

        /// <summary>
        /// 这个TP实体如果在加载世界时已经存在，这个函数会在加载世界时被调用一次，用以用来初始化一些信息
        /// </summary>
        public virtual void LoadInWorld() { }

        /// <summary>
        /// 在世界被卸载时调用一次
        /// </summary>
        public virtual void UnLoadInWorld() { }

        /// <summary>
        /// 这个模块在世界中的存在数量
        /// </summary>
        /// <returns></returns>
        public int GetInWorldHasNum() => TileProcessorLoader.TP_ID_To_InWorld_Count[ID];

        /// <summary>
        /// 这个函数是单实例的，在一个更新周期中，它只会运行一次，如果<see cref="GetInWorldHasNum"/>返回0，就不会被调用
        /// </summary>
        public virtual void SingleInstanceUpdate() {

        }

        /// <summary>
        /// 这个函数在跟随的物块被挖掘或者消失时自动调用一次，
        /// 调用这个函数，将会让模块变得不活跃，同时运行<see cref="OnKill"/>设置死亡事件
        /// </summary>
        public void Kill() {
            OnKill();
            Active = false;

            foreach (var tpGlobal in TileProcessorLoader.TPGlobalHooks) {
                tpGlobal.OnKill(this);
            }

            TrackItem = null;

            TileProcessorLoader.RemoveFromDictionaries(this);
        }

        /// <summary>
        /// 重写这个函数设置一些特殊的死亡事件或者整理数据逻辑。
        /// 一般不直接调用这个函数而是调用<see cref="Kill"/>
        /// </summary>
        public virtual void OnKill() { }

        /// <summary>
        /// 多物块被挖掘时会运行的函数，输入对于动画帧
        /// </summary>
        /// <param name="frameX"></param>
        /// <param name="frameY"></param>
        public virtual void KillMultiTileSet(int frameX, int frameY) { }

        /// <summary>
        /// 游戏加载时调用
        /// </summary>
        public virtual void Load() { }

        /// <summary>
        /// 游戏卸载时调用
        /// </summary>
        public virtual void UnLoad() { }

        /// <summary>
        /// 加载这个TP实体所依赖的物块相关的数据，这个函数一般只在实体生成时调用一次
        /// </summary>
        public virtual void LoadenTile() {
            Tile = Framing.GetTileSafely(Position);
            if (Tile != null && Tile.HasTile) {
                TileObjectData data = TileObjectData.GetTileData(Tile);
                if (data != null) {
                    Width = data.Width * 16;
                    Height = data.Height * 16;
                }
            }
        }

        /// <summary>
        /// 在游戏加载末期调用一次，一般用于设置一些静态的值
        /// </summary>
        public virtual void SetStaticProperty() { }

        /// <summary>
        /// 在模块被生成时调用一次，用于初始化一些实例数据
        /// </summary>
        public virtual void SetProperty() { }

        /// <summary>
        /// 运行在<see cref="Update"/>之前，此时基本数据都已经设置好了，只会在添加进世界时被调用一次，一般用于初始化一些次要数据
        /// 这个函数会在客户端和服务端上运行
        /// </summary>
        public virtual void Initialize() { }

        /// <summary>
        /// 会在所有本地客户端、服务端上更新，编写程序时需要考虑网络结构
        /// </summary>
        public virtual void Update() { }

        /// <summary>
        /// 这个模块在什么情况下应该被标记为死亡
        /// </summary>
        /// <returns></returns>
        public virtual bool IsDaed() {
            //在多人游戏中，不允许客户端自行杀死Tp实体，这些要通过服务器的统一广播来管理
            if (VaultUtils.isClient) {
                return false;
            }

            if (Tile == null) {
                return true;
            }

            if (!Tile.HasTile) {
                return true;
            }

            if (Tile.TileType != TargetTileID) {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 发送数据
        /// </summary>
        public void SendData() => TileProcessorNetWork.TileProcessorSendData(this);

        /// <summary>
        /// 发送数据
        /// </summary>
        public virtual void SendData(ModPacket data) {

        }

        /// <summary>
        /// 接收数据
        /// </summary>
        public virtual void ReceiveData(BinaryReader reader, int whoAmI) {

        }

        /// <summary>
        /// 保存这个实体的数据
        /// </summary>
        public virtual void SaveData(TagCompound tag) {

        }

        /// <summary>
        /// 加载这个实体的数据
        /// </summary>
        public virtual void LoadData(TagCompound tag) {

        }

        /// <summary>
        /// 绘制在物块之前
        /// </summary>
        /// <param name="spriteBatch"></param>
        public virtual void PreTileDraw(SpriteBatch spriteBatch) {

        }

        /// <summary>
        /// 一个独立的绘制函数，在<see cref="TileProcessorSystem.PostDrawTiles"/>中统一运行，该函数的绘制图层在<see cref="Draw"/>下方
        /// </summary>
        /// <param name="spriteBatch"></param>
        public virtual void BackDraw(SpriteBatch spriteBatch) { }

        /// <summary>
        /// 一个独立的绘制函数，在<see cref="TileProcessorSystem.PostDrawTiles"/>中统一运行
        /// </summary>
        /// <param name="spriteBatch"></param>
        public virtual void Draw(SpriteBatch spriteBatch) { }

        /// <summary>
        /// 一个独立的绘制函数，在<see cref="TileProcessorSystem.PostDrawTiles"/>中统一运行，该函数的绘制图层在<see cref="Draw"/>上方
        /// </summary>
        /// <param name="spriteBatch"></param>
        public virtual void FrontDraw(SpriteBatch spriteBatch) { }

        /// <inheritdoc/>
        public override string ToString() => $"Name:{GetType().Name} \nID:{ID} \nwhoAmi:{WhoAmI}";
    }
}
