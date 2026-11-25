public interface IShopSaveStore
{
    bool TryLoad(out ShopSaveData data, out bool exists, out string error);
    bool TrySave(ShopSaveData data, out string error);
}
