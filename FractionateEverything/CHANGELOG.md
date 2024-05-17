- v1.3.1
    + 原版增加分馏配方155个；创世之书、更多巨构、深空来袭同时启用时，增加分馏配方217个。
    + 新增首次加载的弹窗，并提供了跳转创世之书交流群、打开更新日志的功能。
    + 为点数聚集分馏塔、增产分馏塔添加了概率说明，并调整了处理逻辑与概率显示。
        + 对于增产分馏塔，如果输入具有自增值分馏配方，将使用该配方概率进行增产；否则使用默认概率。
        + 对于点数聚集分馏塔，输入物品种类不会影响其效果。优化了概率显示。
    + 将所有分馏塔放在同一个升级链中，现在可以使用升降级自由切换分馏塔类型了。
    + 调整部分翻译。
    + 调整部分物品、配方的位置，调整了部分分馏配方。
        + 添加微型粒子对撞机的自分馏配方。
        + 将创世之书部分物品位置改为与配方一致。
        + 调整了创世之书黑雾物品位置、黑雾物品分馏配方位置。
        + 将创世的玻璃改为循环分馏（相当于增加一个分馏配方）。
        + 为创世之书的巨构建筑添加了循环分馏链，但是没有循环链尾加成。
        + 为创世之书添加高斯机枪塔、导弹防御塔的自分馏配方。
        + 调整了更多巨构中运载火箭的分馏顺序，使其符合科技顺序。
    + 配置文件优化。
        + 移除是否显示分馏图标的开关，添加关闭首次加载弹窗的开关。
        + 现在，除了手动修改配置文件，你也可以在游戏的设置页面修改所有配置。
    + 修复大量bug。
        + 修复语言非中文时，矿脉等显示为中文的问题。
        + 修复精准分馏塔、通用分馏塔前置科技异常，从而导致无法制作这些建筑的问题。
            + 启用创世之书时，通用分馏塔的前置科技描述也随之修改。
        + 修复更多巨构中巨构接收器图标重置、效果丢失的问题。
        + 修复创世之书中导弹防御塔配方隐藏的问题。
    + Added 155 fractionation recipes to the original version; 217 fractionation recipes when Genesis Book, More Mega Structure, and They Come From Void are enabled at the same time.
    + Added a popup window for the first load, and provided the ability to jump to the Genesis Book exchange group
      and open the update log.
    + Added probability description for Points Aggregate Fractionator and Increase Production Fractionator, and adjusted
      processing logic and probability display.
        + For Increase Production Fractionator, if the input has a self-value-added fractionation recipe, the
          probability of the recipe will be used to increase production; otherwise, the default probability will be
          used.
        + For Points Aggregate Fractionators, entering the item type will not affect its effect. Optimized probability
          display.
    + Putting all fractionators in the same upgrade chain now allows you to freely switch fractionator types using
      elevation.
    + Adjusted some translations.
    + Adjusted the location of some items and recipes, and adjusted some fractionation recipes.
        + Added self-fractionation recipe for Micro Particle Collider.
        + Changed the location of some items in the Genesis Book to match the recipe.
        + Adjusted Genesis Book Dark Fog item locations, Dark Fog item fractionation recipe locations.
        + Changed Genesis' glass to circular fractionation (equivalent to adding a fractionation recipe).
        + Added cyclic fractionation chain to Genesis Book' megastructure building, but without the cyclic chain tail
          bonus.
        + Added self-fractionation recipes for Gauss Machine Gun Towers and Missile Defense Towers to the Book of
          Genesis.
        + Adjusted the fractionation order of carrier rockets in More Mega Structure to match the tech order.
    + Configuration file optimization.
        + Removed the switch for whether to show the fractionation icon, and added a switch to turn off the first load
          popup.
        + You can now change all configurations from the game's settings page, in addition to manually changing the
          config files.
    + Fix a lot of bugs.
        + Fixed the problem that veins etc. are displayed in Chinese when the language is not Chinese.
        + Fix an issue where Precision Fractionator and Universal Fractionator had abnormal technology in front of them,
          which prevented them from crafting these buildings.
            + Fixed an issue where Universal Fractionator's tech description was changed when the Book of Creation was
              enabled.
        + Fixed an issue where the Mega Structure Receiver icon was reset and the effect was lost in More Mega
          Structures.
        + Fix an issue where the Missile Defense Tower recipe was hidden in the Genesis Book.

- v1.3.0
    + 适配创世之书、深空来敌、更多巨构。
        + 这些MOD全部启用时，新增配方个数将超过200。
        + 新的配方同样具有三种样式可供选择。
        + 当同时启用创世之书与万物分馏时，将会使用创世独有材料制作分馏塔。
    + 配置文件优化。
        + 移除配置文件中的配方起始ID，现在会自动使用不重复的配方ID。
        + 添加了自动清除配置文件中无用项目的功能。
    + 现在对于任意速率的传送带，分馏塔都可以满速输出了（之前最大速率为30/s）。
    + 优化了分馏塔界面的分馏成功率显示。
        + 分馏成功率从单行改为多行，可以清晰看到所有概率了。
        + 将“F/D”替换为“流动/损毁”。
    + 调整了部分分馏配方。
        + 提升增产剂分馏配方的产物数目（1,1% -> 2,1%），可以更快产出大量增产剂了。
        + 现在所有循环分馏链的最后一个分馏配方（即分馏链尾变分馏链头）将根据分馏链长度提升物品数目。
    + Adapts Genesis Book, They Come From Void, and More Mega Structure.
        + When all of these mods are enabled, the number of new recipes will be over 200.
        + New recipes are also available in three styles.
        + When both Genesis Book and Fractionation of Everything are enabled, fractionators will be crafted using
          materials unique to Genesis.
    + Config file optimization.
        + Removed recipe start IDs from profiles, now automatically uses non-repeating recipe IDs.
        + Added automatic clearing of useless items from config files.
    + The fractionator can now output at full speed for any rate of conveyor (previously the maximum rate was 30/s).
    + Optimized the display of fractionation success rate in the fractionator interface.
        + Fractionation success rate has been changed from single line to multiple lines, so that all probabilities can
          be clearly seen.
        + Replaced "F/D" with "Flow/Destroy".
    + Adjusted some fractionation recipes.
        + Upgraded the number of products for the Enhancer Fractionation recipe (1,1% -> 2,1%), so that you can produce
          large amounts of Enhancers much faster.
        + The last fractionation recipe of all circular fractionation chains (i.e. the end of the fractionation chain
          becomes the head of the fractionation chain) will now boost the number of items based on the length of the
          fractionation chain.

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
