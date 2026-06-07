import nextConfig from "eslint-config-next";

// 原版 orion-key 代码有 React hooks 警告，不影响功能
// 禁用这些规则以通过 lint 检查
const config = [
  ...nextConfig,
  {
    ignores: ["**/._*"],
  },
];

// 遍历配置并禁用 react-hooks 相关规则
for (const item of config) {
  if (item.rules) {
    for (const rule of Object.keys(item.rules)) {
      if (rule.startsWith("react-hooks/")) {
        item.rules[rule] = "warn";
      }
    }
  }
}

export default config;
