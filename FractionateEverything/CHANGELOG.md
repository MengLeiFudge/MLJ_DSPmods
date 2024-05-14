- v1.3.1 preview
    + 新增巨构分馏塔，仅在启用创世之书时加入该建筑。可以将普通建筑分馏为巨构建筑。

- v1.3.0
    + 适配创世之书、深空来敌、更多巨构。这些MOD全部启用时，新增配方个数将超过200。
    + 新的配方同样具有三种样式可供选择。
    + 当同时启用创世之书与万物分馏时，将会使用不同于原版的新配方来制作分馏塔。
    + 移除配置文件中的配方起始ID，现在会自动使用不重复的配方ID。
    + 添加了自动清除配置文件中无用项目的功能。
    + 现在对于任意速率的传送带，分馏塔都可以满速输出了。
    + 优化了分馏塔界面的分馏成功率显示，从单行改为多行，并添加了“流动/损毁”的中英文显示（之前由于位置不够使用F和D代替）。
    + 提升增产剂分馏配方的产物数目，可以更快产出大量增产剂了。
    + Adapts Genesis Book, They Come From Void, and More Mega Structure. When all of these mods are enabled, the
      number of new fractionate recipes will exceed 200.
    + The new recipes also have three styles to choose from.
    + When both Book of Genesis and All Things Fractionated are enabled, a new recipe will be used for the fractionator
      that is different from the original.
    + Removed recipe start IDs from profiles, now automatically uses non-repeating recipe IDs.
    + Added the ability to automatically clear useless items from the config file.
    + The fractionator now outputs at full speed for conveyor belts of any rate.
    + Optimized the fractionation success rate display on the fractionator screen from a single line to multiple lines.
    + Upgraded the number of products in the Increaser Fractionation recipe, so that large amounts of Increaser can be
      produced more quickly.

- v1.2.2
    + 精准分馏塔（原名低功率分馏塔）效果改为缓存的产物越多，分馏效率越低。
    + 新增点数聚集分馏塔，可以将输入物品的增产点数聚集到几个物品上，从而得到10点增产点数的物品。前置科技为新增科技“增产点数聚集”。
    + 增产分馏塔（原名增殖分馏塔）去除本身的10%加成。前置科技为新增隐藏科技“增产分馏”。
    + 点数聚集分馏塔、增产分馏塔可以接受任意物品，无需配方，且产物与输入一致。这次真的万物皆可分馏了！
    + 各种分馏塔现在具有不同的颜色（图标也是如此），可以轻松区分它们了。
    + 新增科技“分馏塔产物集装物流”，研究后，所有分馏塔的产物将以集装形式输出。
    + 增添、移除、调整了部分配方。将所有矩阵分馏配方概率改为1%1个，3%损毁（可以在设置中禁用损毁）。
    + 为所有分馏配方新增了斜线样式、圆弧样式图标。你可以在配置文件中选择三种图标风格之一。
    + 调整了分馏塔的概率显示内容，现在可以准确显示实际的分馏效果。
    + 调整了分馏塔的概率显示刷新频率，从1帧一次减慢至20帧一次，以便清晰看到概率（不会影响建筑处理速率）。
    + The effect of the Precision Fractionator (formerly known as the Low Power Fractionator) has been changed to be
      less efficient at fractionation the more product is cached.
    + Added Points Aggregate Fractionator, which allows you to aggregate the input item's Production Increase Points to
      several items, resulting in a 10 Point Production Increase item. The pre-requisite technology is the new “Yield
      Gathering” technology.
    + Increase Production Fractionator (formerly Increase Production Fractionator) removes its 10% bonus. The new Hidden
      Technology “Fractionation” has been added as a Pre-Tech.
    + Points Aggregate Fractionator and Increase Production Fractionator can accept any item without a recipe, and the
      product is the same as the input. Everything can be fractionated this time!
    + The various fractionators now have different colors (and icons as well), making it easy to tell them apart.
    + Added a new tech called “Fractionator Product Collector Logistics”, which will allow all fractionator products to
      be exported in a collector form.
    + Added, removed, and adjusted some recipes. Changed the probability of all matrix fractionation recipes to 1% for
      1, 3% for destruction (destruction can be disabled in settings).
    + Added new slash style, arc style icons for all fractionation recipes. You can choose one of the three icon styles
      in the config file.
    + Adjusted the probability display content for fractionators, which now accurately displays the actual fractionation
      effect.
    + Adjusted the probability display refresh frequency for fractionators, slowing it down from 1 frame at a time to 20
      frames at a time, so that the probabilities can be seen clearly (without affecting the building processing rate).

- v1.2.0
    + 调整了绝大多数配方的分馏路线！目前新增分馏配方共计155个。其中包括分馏循环链、矿物自分馏等。
    + 为所有分馏配方增加了概率显示。你可以在制作页面查看它们的分馏概率。
    + 将所有配方的图标替换为类似重氢分馏的样式。
    + 物品制作路径中可以看到相关分馏配方了。
    + 修复了解锁科技时，新的分馏配方无法立刻使用的问题。
    + 移除了配置文件中的调整分馏成功率的选项，基础分馏成功率强制使用1%。
    + 原版分馏塔改名为通用分馏塔，建筑效果不变。
    + 增加低功率分馏塔，耗电量变为1/5，分馏成功率变为1/3。
    + 增加建筑极速分馏塔，输入建筑时有12.5%概率分馏成功，否则仅有0.1%概率分馏成功。
    + 增加增殖分馏塔，每次分馏成功有10%概率将输出物品数目翻倍，且增产点数视为增产而非加速。
    + Adjusted the fractionation routes for the vast majority of recipes! A total of 155 new fractionation recipes have
      now been added. These include Fractionation Cycle Chain, Mineral Self-Fractionation, and more.
    + Added probability display for all fractionation recipes. You can view their fractionation probabilities on the
      crafting page.
    + Replaced the icons for all recipes with a style similar to deuterium fractionation.
    + Related fractionation recipes are now visible in the item crafting path.
    + Fixed an issue where new fractionation recipes were not immediately available when unlocking tech.
    + Removed the option to adjust the fractionation success rate in the config file, and forced the base fractionation
      success rate to be 1%.
    + Original Fractionation Tower renamed to Universal Fractionator, building effect remains unchanged.
    + Add low power fractionation tower, power consumption becomes 1/5, fractionation success rate becomes 1/3.
    + Add Building-HighSpeed Fractionator, 12.5% probability of fractionation success when building is entered,
      otherwise only 0.1% probability of fractionation success.
    + Add Augmentation fractionator, each successful fractionation has a 10% chance of doubling the number of output
      items, and augmentation points are treated as augmentation instead of acceleration.

- v1.1.0
    + 适配版本 0.10.29.22015。
    + 新增了1个分馏配方，目前新增分馏配方共计105个。
    + 为每个分馏配方添加了图标显示，图标为产物图标。
    + 为每个分馏配方添加了前置科技，解锁科技将会同步解锁相应的分馏配方。
    + 允许自定义是否启用前置科技。如果不启用，所有分馏配方将在开局可用。
    + 允许自定义是否显示所有的分馏配方。
    + 允许自定义从哪一页开始显示新的分馏配方（用于避免mod之间可能存在的配方显示冲突）。
    + 允许自定义从哪个ID开始添加分馏配方（用于避免mod之间可能存在的配方ID冲突）。
    + Updated to work with game version 0.10.29.22015.
    + 1 new fractionation recipe has been added, for a current total of 105 new fractionation recipes.
    + Pre-techs have been added for each fractionation recipe, and unlocking a tech will unlock the corresponding
      fractionation recipe at the same time.
    + Allows customization of whether previous technology is enabled. If not enabled, all fractionation recipes will be
      available at the beginning.
    + Add icon display for each fractionated recipe.
    + Allow customization of whether all fractionated recipes are displayed.
    + Allow to customize from which page new fractionated recipes are displayed (used to avoid possible recipe display
      conflicts between mods).
    + Allow to customize from which ID to start adding fractionated recipes (used to avoid possible recipe ID conflicts
      between mods).

- v1.0.1
    + 适配版本 0.10.29.21950。
    + 新增了3个分馏配方，目前新增分馏配方共计104个。
    + Updated to work with game version 0.10.29.21950.
    + 3 new fractionation recipes has been added, for a current total of 105 new fractionation recipes.

- v1.0.0
    + 适配版本 0.10.28.21014。
    + 新增了一些分馏配方，包括新的武器、物品等。目前新增分馏配方共计101个。
    + Updated to work with game version 0.10.28.21014.
    + Add some fractionate recipes with new buildings and items. A total of 101 new fractionation recipes have now been
      added.
